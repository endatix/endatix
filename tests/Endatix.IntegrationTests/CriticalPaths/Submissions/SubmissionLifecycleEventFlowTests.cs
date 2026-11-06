using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// Every create entry point captures one <c>submission.created</c> outbox row, every lifecycle change one row named
/// by its phases on its own revision, and in-progress answer saves on an active submission none.
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
        Assert.Equal(new SubmissionOutboxRow(SubmissionLifecycleEvent.EventTypes.Created, CollectionStatusCodes.InProgress, 2, false), row);
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
        Assert.Equal(SubmissionLifecycleEvent.EventTypes.Created, row.EventType);
        Assert.Equal(CollectionStatusCodes.NotStarted, row.CollectionStatus);
    }

    [Fact]
    public async Task Admin_create_of_a_complete_submission_captures_created_then_completed_on_their_own_revisions()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateEnabledFormAsync(admin);

        // Act
        var created = await CreateAsync(admin, $"/api/forms/{formId}/submissions/onbehalf", isComplete: true);

        // Assert
        var rows = await RowsAsync(world, created.Id);
        Assert.Equal(
            [
                new SubmissionOutboxRow(SubmissionLifecycleEvent.EventTypes.Created, CollectionStatusCodes.Complete, 2, false),
                new SubmissionOutboxRow(SubmissionLifecycleEvent.EventTypes.Completed, CollectionStatusCodes.Complete, 3, false),
            ],
            rows);
    }

    [Fact]
    public async Task Public_create_with_a_screen_out_captures_created_in_screen_out_then_closed()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateEnabledFormAsync(admin);

        // Act
        var created = await CreateScreenedOutAsync(admin, $"/api/forms/{formId}/submissions");

        // Assert
        Assert.Equal(
            [
                new SubmissionOutboxRow(SubmissionLifecycleEvent.EventTypes.Created, CollectionStatusCodes.ScreenOut, 2, false),
                new SubmissionOutboxRow(SubmissionLifecycleEvent.EventTypes.Closed, CollectionStatusCodes.ScreenOut, 3, false),
            ],
            await RowsAsync(world, created.Id));
    }

    [Fact]
    public async Task Public_create_with_an_unregistered_outcome_is_a_400()
    {
        // Arrange
        var (_, admin) = await AdminAsync();
        var formId = await CreateEnabledFormAsync(admin);

        // Act
        using var response = await admin.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions",
            new { isComplete = false, jsonData = "{}", collectionOutcome = "not_a_registered_code" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task First_save_of_a_prefilled_submission_captures_started()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateEnabledFormAsync(admin);
        var created = await CreateAsync(admin, $"/api/forms/{formId}/submissions/onbehalf", isComplete: false);

        // Act
        await PatchAsync(admin, $"/api/forms/{formId}/submissions/{created.Id}", new { jsonData = """{"age":"1"}""", isComplete = false });

        // Assert
        Assert.Equal(
            [
                new SubmissionOutboxRow(SubmissionLifecycleEvent.EventTypes.Created, CollectionStatusCodes.NotStarted, 2, false),
                new SubmissionOutboxRow(SubmissionLifecycleEvent.EventTypes.Started, CollectionStatusCodes.InProgress, 3, false),
            ],
            await RowsAsync(world, created.Id));
    }

    [Fact]
    public async Task In_progress_saves_capture_nothing_and_completing_captures_one_completed()
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

        // Assert — one completed event on the next revision
        var rows = await RowsAsync(world, created.Id);
        Assert.Equal(
            [
                new SubmissionOutboxRow(SubmissionLifecycleEvent.EventTypes.Created, CollectionStatusCodes.InProgress, 2, false),
                new SubmissionOutboxRow(SubmissionLifecycleEvent.EventTypes.Completed, CollectionStatusCodes.Complete, 3, false),
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

    private static Task<SubmissionApiModel> CreateAsync(HttpClient client, string url, bool isComplete) =>
        PostAsync(client, url, new { isComplete, jsonData = """{"age":"18_or_over"}""", currentPage = 0 });

    private static Task<SubmissionApiModel> CreateScreenedOutAsync(HttpClient client, string url) =>
        PostAsync(client, url, new { isComplete = false, jsonData = """{"age":"12"}""", currentPage = 0, collectionOutcome = CollectionStatusCodes.ScreenOut });

    private static async Task<SubmissionApiModel> PostAsync(HttpClient client, string url, object body)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await client.PostAsJsonAsync(url, body, cancellationToken);
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
