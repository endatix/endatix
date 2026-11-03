using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// Every create entry point captures one <c>submission.created</c> outbox row, every collection status change
/// one <c>submission.collection_status_changed</c> row on one revision, and in-progress answer saves none.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "CriticalPath")]
[Trait("Priority", "P0")]
public sealed class SubmissionLifecycleEventFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Public_create_captures_one_created_row_in_progress()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateEnabledFormAsync(admin);
        await PatchAsync(admin, $"/api/forms/{formId}", new { isPublic = true });
        using var anonymous = world.AnonymousClient();

        // Act
        var created = await CreateAsync(anonymous, $"/api/forms/{formId}/submissions", isComplete: false);

        // Assert
        var rows = await RowsAsync(world, created.Id);
        var row = Assert.Single(rows);
        Assert.Equal(new SubmissionOutboxRow(SubmissionCreatedEvent.EventTypeName, CollectionStatusCodes.InProgress, 2, false), row);
    }

    [Fact]
    public async Task Prefilled_create_on_behalf_captures_one_created_row_not_started()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateEnabledFormAsync(admin);

        // Act
        var created = await CreateAsync(admin, $"/api/forms/{formId}/submissions/onbehalf", isComplete: false);

        // Assert
        var row = Assert.Single(await RowsAsync(world, created.Id));
        Assert.Equal(SubmissionCreatedEvent.EventTypeName, row.EventType);
        Assert.Equal(CollectionStatusCodes.NotStarted, row.CollectionStatus);
    }

    [Fact]
    public async Task Admin_create_of_a_complete_submission_captures_created_then_completed_on_one_revision()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateEnabledFormAsync(admin);

        // Act
        var created = await CreateAsync(admin, $"/api/forms/{formId}/submissions/onbehalf", isComplete: true);

        // Assert
        var rows = await RowsAsync(world, created.Id);
        Assert.Equal(
            [SubmissionCreatedEvent.EventTypeName, SubmissionCompletedEvent.EventTypeName],
            rows.Select(row => row.EventType));
        Assert.All(rows, row => Assert.Equal(2, row.Revision));
        Assert.Equal(CollectionStatusCodes.Complete, rows[0].CollectionStatus);
    }

    [Fact]
    public async Task In_progress_saves_capture_nothing_and_completing_captures_one_transition_and_completed()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateEnabledFormAsync(admin);
        var created = await CreateAsync(admin, $"/api/forms/{formId}/submissions", isComplete: false);
        var byToken = $"/api/forms/{formId}/submissions/by-token/{created.Token}";

        // Act — three in-progress answer saves
        for (var page = 1; page <= 3; page++)
        {
            await PatchAsync(admin, byToken, new { jsonData = $$"""{"age":"{{page}}"}""", currentPage = page });
        }

        // Assert — still only the created row
        Assert.Single(await RowsAsync(world, created.Id));

        // Act — complete
        await PatchAsync(admin, byToken, new { jsonData = """{"age":"done"}""", currentPage = 3, isComplete = true });

        // Assert — one transition and the completed event, both on the next revision
        var rows = await RowsAsync(world, created.Id);
        Assert.Equal(
            [
                new SubmissionOutboxRow(SubmissionCreatedEvent.EventTypeName, CollectionStatusCodes.InProgress, 2, false),
                new SubmissionOutboxRow(SubmissionCollectionStatusChangedEvent.EventTypeName, CollectionStatusCodes.Complete, 3, false),
                new SubmissionOutboxRow(SubmissionCompletedEvent.EventTypeName, null, 3, false),
            ],
            rows);
    }

    private async Task<(IntegrationTestWorld World, HttpClient Admin)> AdminAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword },
            cancellationToken);
        return (world, await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken));
    }

    private static Task<IReadOnlyList<SubmissionOutboxRow>> RowsAsync(IntegrationTestWorld world, string submissionId) =>
        SubmissionOutboxRows.ReadAsync(world.Services, long.Parse(submissionId), TestContext.Current.CancellationToken);

    private static async Task<SubmissionApiModel> CreateAsync(HttpClient client, string url, bool isComplete)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await client.PostAsJsonAsync(
            url,
            new { isComplete, jsonData = """{"age":"18_or_over"}""", currentPage = 0 },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var model = await response.Content.ReadFromJsonAsync<SubmissionApiModel>(_jsonOptions, cancellationToken);
        Assert.NotNull(model);
        return model;
    }

    private static async Task PatchAsync(HttpClient client, string url, object body)
    {
        using var response = await client.PatchAsJsonAsync(url, body, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<string> CreateEnabledFormAsync(HttpClient client)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await client.PostAsJsonAsync(
            "/api/forms",
            new
            {
                name = $"lifecycle-form-{Guid.NewGuid():N}",
                isEnabled = true,
                formDefinitionJsonData = """{"pages":[{"name":"page1","elements":[{"type":"text","name":"age"}]}]}"""
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var form = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return form.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Form create response missing id.");
    }

    private sealed class SubmissionApiModel
    {
        public string Id { get; set; } = string.Empty;
        public string? Token { get; set; }
    }
}
