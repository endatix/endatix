using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// A deleted form keeps no live submission and takes no write to one: not from a create that overlaps the delete
/// (<c>FormDeletionFlowTests.Races.cs</c>), and not to a submission an earlier version left live under the form.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "CriticalPath")]
[Trait("Priority", "P0")]
public sealed partial class FormDeletionFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";

    private const string FormDefinition =
        """{"pages":[{"name":"page1","elements":[{"type":"text","name":"q"}]}]}""";

    private static readonly object DraftSubmission = new { isComplete = false, jsonData = "{}", currentPage = 0 };

    public static TheoryData<string, string, string> SubmissionWrites => new()
    {
        { "PATCH", "", """{"jsonData":"{\"q\":\"changed\"}"}""" },
        { "PATCH", "", """{"isComplete":true,"jsonData":"{\"q\":\"done\"}"}""" },
        { "PUT", "", """{"isComplete":false,"jsonData":"{\"q\":\"replaced\"}","currentPage":1}""" },
        { "POST", "/status", $$"""{"status":"{{SubmissionStatusCodes.Read}}"}""" },
    };

    /// <summary>
    /// The submissions go by form in one statement, not one by one: each is stamped deleted, and the form's own
    /// deletion event is the only one raised for them.
    /// </summary>
    [Fact]
    public async Task Deleting_a_form_deletes_every_submission_and_announces_only_the_form()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateFormAsync(admin);
        long[] submissionIds = [await CreateSubmissionAsync(admin, formId), await CreateSubmissionAsync(admin, formId)];
        var before = DateTime.UtcNow;

        // Act
        using var deleted = await admin.DeleteAsync($"/api/forms/{formId}", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        var rows = await DeletionStampsAsync(world.Services, formId);
        Assert.Equal(submissionIds.Order(), rows.Select(row => row.Id).Order());
        Assert.All(rows, row => Assert.True(row.IsDeleted && row.DeletedAt >= before && row.ModifiedAt == row.DeletedAt));
        Assert.Equal([FormDeletedEvent.EventTypeName], await EventTypesNamingAsync(world.Services, formId, before));
    }

    /// <summary>
    /// Earlier versions could leave a submission live under a deleted form. Writes to it are refused as if it were
    /// gone, so it can no longer change or complete and raise its events.
    /// </summary>
    [Theory]
    [MemberData(nameof(SubmissionWrites))]
    public async Task Writing_to_a_submission_left_live_under_a_deleted_form_is_not_found(
        string method,
        string pathSuffix,
        string body)
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateFormAsync(admin);
        var submissionId = await CreateSubmissionAsync(admin, formId);
        using var deleted = await admin.DeleteAsync($"/api/forms/{formId}", Ct);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        await LeaveLiveAsync(world.Services, submissionId);
        using var request = WriteRequest(new SubmissionWrite(formId, submissionId, method, pathSuffix, body));

        // Act
        using var response = await admin.SendAsync(request, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertUnchangedAsync(world.Services, submissionId);
    }

    /// <summary>
    /// A form row soft-deleted with its definition and submission left live, as only the form's own filter can tell:
    /// the definition join that hides the usual leftovers does not hide this one.
    /// </summary>
    [Fact]
    public async Task Updating_a_submission_whose_form_alone_is_deleted_is_not_found()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateFormAsync(admin);
        var submissionId = await CreateSubmissionAsync(admin, formId);
        await DeleteOnlyTheFormRowAsync(world.Services, formId);
        using var request = WriteRequest(new SubmissionWrite(formId, submissionId, "PATCH", "", """{"jsonData":"{\"q\":\"changed\"}"}"""));

        // Act
        using var response = await admin.SendAsync(request, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertUnchangedAsync(world.Services, submissionId);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record SubmissionWrite(long FormId, long SubmissionId, string Method, string PathSuffix, string Body);

    private static HttpRequestMessage WriteRequest(SubmissionWrite write) =>
        new(new HttpMethod(write.Method), $"/api/forms/{write.FormId}/submissions/{write.SubmissionId}{write.PathSuffix}")
        {
            Content = new StringContent(write.Body, System.Text.Encoding.UTF8, "application/json"),
        };

    private async Task<(IntegrationTestWorld World, HttpClient Admin)> AdminAsync()
    {
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword },
            Ct);
        return (world, await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: Ct));
    }

    private static async Task<long> CreateFormAsync(HttpClient admin)
    {
        using var response = await admin.PostAsJsonAsync(
            "/api/forms",
            new { name = $"deleted-form-{Guid.NewGuid():N}", isEnabled = true, formDefinitionJsonData = FormDefinition },
            Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await IdAsync(response);
    }

    private static async Task<long> CreateSubmissionAsync(HttpClient admin, long formId)
    {
        using var response = await admin.PostAsJsonAsync($"/api/forms/{formId}/submissions", DraftSubmission, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await IdAsync(response);
    }

    private static async Task<long> IdAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return long.Parse(body.GetProperty("id").GetString()!);
    }

    /// <summary>Undoes the submission's delete only, the state a delete that missed it left.</summary>
    private static async Task LeaveLiveAsync(IServiceProvider services, long submissionId)
    {
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await AllSubmissions(app)
            .Where(row => row.Id == submissionId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.IsDeleted, false)
                    .SetProperty(row => row.DeletedAt, (DateTime?)null),
                Ct);
        Assert.Equal(1, updated);
    }

    private static async Task DeleteOnlyTheFormRowAsync(IServiceProvider services, long formId)
    {
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await app.Forms
            .IgnoreQueryFilters([EndatixQueryFilterNames.Tenant])
            .Where(form => form.Id == formId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(form => form.IsDeleted, true), Ct);
        Assert.Equal(1, updated);
    }

    private static async Task AssertUnchangedAsync(IServiceProvider services, long submissionId)
    {
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await AllSubmissions(app).AsNoTracking().SingleAsync(item => item.Id == submissionId, Ct);
        Assert.Equal("{}", row.JsonData);
        Assert.False(row.IsComplete);
        Assert.Equal(SubmissionStatusCodes.New, row.Status.Code);
    }

    /// <summary>Whether each submission of the form is soft-deleted, by id.</summary>
    private static async Task<Dictionary<long, bool>> DeletedByIdAsync(IServiceProvider services, long formId)
    {
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await AllSubmissions(app)
            .AsNoTracking()
            .Where(row => row.FormId == formId)
            .ToDictionaryAsync(row => row.Id, row => row.IsDeleted, Ct);
    }

    private sealed record DeletionStamp(long Id, bool IsDeleted, DateTime? DeletedAt, DateTime? ModifiedAt);

    private static async Task<List<DeletionStamp>> DeletionStampsAsync(IServiceProvider services, long formId)
    {
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await AllSubmissions(app)
            .AsNoTracking()
            .Where(row => row.FormId == formId)
            .Select(row => new DeletionStamp(row.Id, row.IsDeleted, row.DeletedAt, row.ModifiedAt))
            .ToListAsync(Ct);
    }

    /// <summary>The types of the outbox events captured since <paramref name="since"/> whose payload names the id.</summary>
    private static async Task<List<string>> EventTypesNamingAsync(IServiceProvider services, long id, DateTime since)
    {
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var idText = id.ToString(CultureInfo.InvariantCulture);
        var messages = await app.OutboxMessages
            .AsNoTracking()
            .Where(message => message.OccurredAt >= since)
            .Select(message => new { message.EventType, message.Payload })
            .ToListAsync(Ct);

        // The payload is jsonb on PostgreSQL, so it is matched here rather than in the query.
        return messages
            .Where(message => message.Payload.Contains(idText, StringComparison.Ordinal))
            .Select(message => message.EventType)
            .ToList();
    }

    private static IQueryable<Submission> AllSubmissions(AppDbContext app) =>
        app.Submissions.IgnoreQueryFilters([EndatixQueryFilterNames.SoftDelete, EndatixQueryFilterNames.Tenant]);
}
