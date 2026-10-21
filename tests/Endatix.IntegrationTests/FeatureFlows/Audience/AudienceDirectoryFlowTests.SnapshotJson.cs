using System.Net;
using System.Text.Json.Nodes;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// The migration that stores the Snapshot as JSON (endatix#1340), run down and up on the active provider against a
/// Snapshot an earlier version stored as text.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    private const string SnapshotJsonMigration = "StorePersonalizationSnapshotAsJson";

    [Fact]
    public async Task A_snapshot_stored_as_text_converts_without_loss_and_still_names_its_participant()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await FormWithCityAsync(admin);
        var tester = await AddTestParticipantAsync(admin, formId, "tester@example.com");
        var testId = IdOf(await OpenTestSubmissionAsync(admin, formId, tester.MembershipId), "submissionId");
        var readBefore = await PersonalizationOfAsync(admin, formId, testId);
        await MigrateToBeforeSnapshotJsonAsync(world.Services);
        var legacy = JsonNode.Parse(await StoredSnapshotAsync(world.Services, testId))!.ToJsonString(Indented);
        await StoreSnapshotTextAsync(world.Services, testId, legacy);

        // Act
        await MigrateToLatestAsync(world.Services);

        // Assert
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(legacy), JsonNode.Parse(await StoredSnapshotAsync(world.Services, testId))));
        Assert.Equal(readBefore, await PersonalizationOfAsync(admin, formId, testId));
        var participant = await GetParticipantAsync(admin, formId, tester.MemberId);
        Assert.Equal([(testId, "test", true)], SubmissionRows(participant));
    }

    private static readonly System.Text.Json.JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>The submission's Snapshot read as the API returns it.</summary>
    private static async Task<string> PersonalizationOfAsync(AudienceApi admin, long formId, long submissionId)
    {
        using var response = await admin.Client.GetAsync(
            $"/api/forms/{formId}/submissions/{submissionId}", admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await admin.ReadAsync(response)).GetProperty("personalization").GetRawText();
    }

    private static async Task MigrateToBeforeSnapshotJsonAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ids = app.GetService<IMigrationsAssembly>().Migrations.Keys.ToList();
        var index = ids.FindIndex(id => id.EndsWith($"_{SnapshotJsonMigration}", StringComparison.Ordinal));
        Assert.True(index > 0, $"No {SnapshotJsonMigration} migration on {app.Database.ProviderName}.");
        await app.GetService<IMigrator>().MigrateAsync(ids[index - 1], TestContext.Current.CancellationToken);
    }

    private static async Task MigrateToLatestAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await app.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Writes the column as text, the way it was stored before the migration; quoted names work on both providers.</summary>
    private static async Task StoreSnapshotTextAsync(IServiceProvider services, long submissionId, string snapshot)
    {
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await app.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "Submissions" SET "PersonalizationSnapshot" = {snapshot} WHERE "Id" = {submissionId}""",
            TestContext.Current.CancellationToken);
    }

    private static async Task<string> StoredSnapshotAsync(IServiceProvider services, long submissionId)
    {
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var snapshot = await app.Submissions.IgnoreQueryFilters([EndatixQueryFilterNames.Tenant]).AsNoTracking()
            .Where(submission => submission.Id == submissionId)
            .Select(submission => submission.PersonalizationSnapshot)
            .SingleAsync(TestContext.Current.CancellationToken);
        return Assert.IsType<string>(snapshot);
    }
}
