using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// The respondent's open (<c>POST forms/{formId}/submissions/open</c>) records <c>startedAt</c> once: it creates a
/// <c>viewed</c> submission when there is none, and a resume by token keeps the first open's time.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "CriticalPath")]
[Trait("Priority", "P0")]
public sealed class SubmissionOpenFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DateTime _longAgo = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Open_without_a_token_creates_a_viewed_submission_with_its_start()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreatePublicFormAsync(admin);
        using var anonymous = world.AnonymousClient();

        // Act
        using var response = await anonymous.PostAsJsonAsync(OpenUrl(formId), new { }, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var opened = await ReadAsync(response);
        Assert.Equal(CollectionStatusCodes.Viewed, opened.CollectionStatus);
        Assert.NotNull(opened.StartedAt);
        Assert.False(string.IsNullOrEmpty(opened.Token));
        Assert.Equal(
            [new SubmissionOutboxRow(SubmissionLifecycleEvent.EventTypes.Created, CollectionStatusCodes.Viewed, 2, false)],
            await RowsAsync(world, opened.Id));
    }

    [Fact]
    public async Task Resume_by_token_keeps_the_start_and_moves_modified_at()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreatePublicFormAsync(admin);
        using var anonymous = world.AnonymousClient();
        var first = await OpenAsync(anonymous, formId, token: null, HttpStatusCode.Created);
        await BackdateAsync(world, first.Id);

        // Act
        var resumed = await OpenAsync(anonymous, formId, first.Token, HttpStatusCode.OK);

        // Assert
        var stored = await StoredAsync(world, first.Id);
        Assert.Equal(_longAgo, stored.StartedAt);
        Assert.Equal(_longAgo, stored.CreatedAt);
        Assert.True(stored.ModifiedAt > _longAgo);
        Assert.Equal(_longAgo, resumed.StartedAt);
        Assert.Equal(CollectionStatusCodes.Viewed, stored.CollectionStatus);
        Assert.Single(await RowsAsync(world, first.Id));
    }

    [Fact]
    public async Task Open_of_an_on_behalf_submission_moves_it_to_viewed_and_the_first_answer_starts_it()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreatePublicFormAsync(admin);
        var created = await PostAsync(admin, $"/api/forms/{formId}/submissions/onbehalf", new { isComplete = false, jsonData = "{}" });
        Assert.Null(created.StartedAt);
        await BackdateAsync(world, created.Id);
        using var anonymous = world.AnonymousClient();

        // Act
        var opened = await OpenAsync(anonymous, formId, created.Token, HttpStatusCode.OK);
        await PatchAsync(anonymous, $"/api/forms/{formId}/submissions/by-token/{created.Token}", new { jsonData = """{"q1":"a"}""", isComplete = false });

        // Assert
        Assert.Equal(CollectionStatusCodes.Viewed, opened.CollectionStatus);
        var stored = await StoredAsync(world, created.Id);
        Assert.Equal(DateTimeTestHelpers.TruncateToMicroseconds(opened.StartedAt!.Value), stored.StartedAt);
        Assert.Equal(_longAgo, stored.CreatedAt);
        Assert.Equal(CollectionStatusCodes.InProgress, stored.CollectionStatus);
        Assert.Equal(
            [
                new SubmissionOutboxRow(SubmissionLifecycleEvent.EventTypes.Created, CollectionStatusCodes.NotStarted, 2, false),
                new SubmissionOutboxRow(SubmissionLifecycleEvent.EventTypes.CollectionStatusMoved, CollectionStatusCodes.Viewed, 3, false),
                new SubmissionOutboxRow(SubmissionLifecycleEvent.EventTypes.Started, CollectionStatusCodes.InProgress, 4, false),
            ],
            await RowsAsync(world, created.Id));
    }

    [Fact]
    public async Task Open_with_a_share_link_token_moves_an_on_behalf_submission_to_viewed()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateEnabledFormAsync(admin);
        var created = await PostAsync(admin, $"/api/forms/{formId}/submissions/onbehalf", new { isComplete = false, jsonData = "{}" });
        await BackdateAsync(world, created.Id);
        var shareToken = await AccessTokenAsync(admin, formId, created.Id, "submit");
        using var anonymous = world.AnonymousClient();

        // Act
        using var response = await anonymous.PostAsJsonAsync(OpenUrl(formId), new { accessToken = shareToken }, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await StoredAsync(world, created.Id);
        Assert.Equal(CollectionStatusCodes.Viewed, stored.CollectionStatus);
        Assert.NotNull(stored.StartedAt);
        Assert.Equal(_longAgo, stored.CreatedAt);
        Assert.True(stored.ModifiedAt > _longAgo);
    }

    [Fact]
    public async Task Open_with_a_staff_view_token_is_refused_and_writes_nothing()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateEnabledFormAsync(admin);
        var created = await PostAsync(admin, $"/api/forms/{formId}/submissions/onbehalf", new { isComplete = false, jsonData = "{}" });
        await BackdateAsync(world, created.Id);
        var viewToken = await AccessTokenAsync(admin, formId, created.Id, "view", "edit");
        using var anonymous = world.AnonymousClient();

        // Act
        using var response = await anonymous.PostAsJsonAsync(OpenUrl(formId), new { accessToken = viewToken }, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var stored = await StoredAsync(world, created.Id);
        Assert.Equal(CollectionStatusCodes.NotStarted, stored.CollectionStatus);
        Assert.Null(stored.StartedAt);
        Assert.Equal(_longAgo, stored.ModifiedAt);
    }

    [Fact]
    public async Task Open_with_both_tokens_is_a_400()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreatePublicFormAsync(admin);
        using var anonymous = world.AnonymousClient();

        // Act
        using var response = await anonymous.PostAsJsonAsync(OpenUrl(formId), new { submissionToken = "a", accessToken = "b" }, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Open_of_a_complete_submission_changes_nothing()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreatePublicFormAsync(admin);
        var created = await PostAsync(admin, $"/api/forms/{formId}/submissions/onbehalf", new { isComplete = true, jsonData = "{}" });
        await BackdateAsync(world, created.Id);
        using var anonymous = world.AnonymousClient();

        // Act
        using var response = await anonymous.PostAsJsonAsync(OpenUrl(formId), new { submissionToken = created.Token }, Ct);

        // Assert — the open answers as the read by token does, and writes nothing
        using var read = await anonymous.GetAsync($"/api/forms/{formId}/submissions/by-token/{created.Token}", Ct);
        Assert.Equal(read.StatusCode, response.StatusCode);
        var stored = await StoredAsync(world, created.Id);
        Assert.Equal(_longAgo, stored.StartedAt);
        Assert.Equal(_longAgo, stored.ModifiedAt);
        Assert.Equal(2, (await RowsAsync(world, created.Id)).Count);
    }

    [Fact]
    public async Task Open_with_an_unknown_token_is_a_400()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreatePublicFormAsync(admin);
        using var anonymous = world.AnonymousClient();

        // Act
        using var response = await anonymous.PostAsJsonAsync(OpenUrl(formId), new { submissionToken = "not-a-token" }, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Open_of_a_disabled_form_is_a_404()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreatePublicFormAsync(admin);
        var created = await OpenAsync(world.AnonymousClient(), formId, token: null, HttpStatusCode.Created);
        await PatchAsync(admin, $"/api/forms/{formId}", new { isEnabled = false });
        using var anonymous = world.AnonymousClient();

        // Act
        using var create = await anonymous.PostAsJsonAsync(OpenUrl(formId), new { }, Ct);
        using var resume = await anonymous.PostAsJsonAsync(OpenUrl(formId), new { submissionToken = created.Token }, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, resume.StatusCode);
    }

    [Fact]
    public async Task Anonymous_open_of_a_private_form_creates_nothing()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateEnabledFormAsync(admin);
        using var anonymous = world.AnonymousClient();

        // Act
        using var response = await anonymous.PostAsJsonAsync(OpenUrl(formId), new { }, Ct);

        // Assert
        Assert.False(response.IsSuccessStatusCode);
        await using var scope = world.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Submissions.AnyAsync(row => row.FormId == long.Parse(formId), Ct));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string OpenUrl(string formId) => $"/api/forms/{formId}/submissions/open";

    private async Task<(IntegrationTestWorld World, HttpClient Admin)> AdminAsync()
    {
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, Ct);
        return (world, await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: Ct));
    }

    private static Task<IReadOnlyList<SubmissionOutboxRow>> RowsAsync(IntegrationTestWorld world, string submissionId) =>
        SubmissionOutboxRows.ReadAsync(world.Services, long.Parse(submissionId), Ct);

    private static async Task<SubmissionApiModel> OpenAsync(HttpClient client, string formId, string? token, HttpStatusCode expected)
    {
        using var response = await client.PostAsJsonAsync(OpenUrl(formId), new { submissionToken = token }, Ct);
        Assert.Equal(expected, response.StatusCode);
        return await ReadAsync(response);
    }

    private static async Task<string> AccessTokenAsync(HttpClient admin, string formId, string submissionId, params string[] permissions)
    {
        using var response = await admin.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions/{submissionId}/access-token",
            new { expiryMinutes = 60, permissions },
            Ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return body.GetProperty("token").GetString() ?? throw new InvalidOperationException("Access token response missing token.");
    }

    private static async Task<SubmissionApiModel> PostAsync(HttpClient client, string url, object body)
    {
        using var response = await client.PostAsJsonAsync(url, body, Ct);
        response.EnsureSuccessStatusCode();
        return await ReadAsync(response);
    }

    private static async Task PatchAsync(HttpClient client, string url, object body)
    {
        using var response = await client.PatchAsJsonAsync(url, body, Ct);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<SubmissionApiModel> ReadAsync(HttpResponseMessage response)
    {
        var model = await response.Content.ReadFromJsonAsync<SubmissionApiModel>(_jsonOptions, Ct);
        Assert.NotNull(model);
        return model;
    }

    /// <summary>
    /// Moves the row's timestamps far into the past, so a later write is told apart from the first. A start already
    /// recorded moves back too; an empty one stays empty.
    /// </summary>
    private static async Task BackdateAsync(IntegrationTestWorld world, string submissionId)
    {
        var id = long.Parse(submissionId);
        await using var scope = world.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await db.Submissions
            .Where(row => row.Id == id)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(row => row.CreatedAt, _longAgo)
                    .SetProperty(row => row.ModifiedAt, _longAgo)
                    .SetProperty(row => row.StartedAt, row => row.StartedAt == null ? (DateTime?)null : _longAgo),
                Ct);
        Assert.Equal(1, updated);
    }

    private static async Task<StoredSubmission> StoredAsync(IntegrationTestWorld world, string submissionId)
    {
        var id = long.Parse(submissionId);
        await using var scope = world.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Submissions.AsNoTracking()
            .Where(row => row.Id == id)
            .Select(row => new StoredSubmission(row.CreatedAt, row.ModifiedAt, row.StartedAt, row.CollectionStatus.Code))
            .SingleAsync(Ct);
    }

    private static async Task<string> CreatePublicFormAsync(HttpClient admin)
    {
        var formId = await CreateEnabledFormAsync(admin);
        await PatchAsync(admin, $"/api/forms/{formId}", new { isPublic = true });
        return formId;
    }

    private static async Task<string> CreateEnabledFormAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/forms",
            new
            {
                name = $"open-form-{Guid.NewGuid():N}",
                isEnabled = true,
                formDefinitionJsonData = """{"pages":[{"name":"page1","elements":[{"type":"text","name":"q1"}]}]}"""
            },
            Ct);
        response.EnsureSuccessStatusCode();
        var form = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return form.GetProperty("id").GetString() ?? throw new InvalidOperationException("Form create response missing id.");
    }

    private sealed record StoredSubmission(DateTime CreatedAt, DateTime? ModifiedAt, DateTime? StartedAt, string CollectionStatus);

    private sealed class SubmissionApiModel
    {
        public string Id { get; set; } = string.Empty;
        public string? Token { get; set; }
        public DateTime? StartedAt { get; set; }
        public string CollectionStatus { get; set; } = string.Empty;
    }
}
