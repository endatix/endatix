using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

public sealed partial class FlattenedSubmissionRepositoryTests
{
    public enum Snapshot { Matching, TimestampBefore, TimestampAfter, NoAnswers, Deleted, Failed, OlderRevision, Forced }

    [Fact]
    public async Task TerminalWrites_AfterSupersessionOrDeletion_NeverChangeTheRow()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(ct);
        await using var db = CreateContext(TenantId);
        var rows = CreateRepository(db);
        var key = KeyOf(SubmissionId);
        await rows.EnsureExistsAsync(key, ct);
        var old = await ClaimAsync(rows, WriteOf(3), ct);
        var replacement = await ClaimAsync(rows, WriteOf(3), ct);

        // Act & assert: neither an independently reconstructed value nor the superseded owner can finish.
        await AssertCannotFinishAsync(rows, WriteOf(3), ct);
        await AssertCannotFinishAsync(rows, old, ct);
        await AssertCannotFinishAsync(rows, replacement with { Revision = 4 }, ct);
        (await ReadRowAsync(rows, ct)).FlattenAttemptId.Should().Be(replacement.AttemptId);
        (await rows.TryMarkProcessedAsync(replacement, ProcessedData, ct)).Should().BeTrue();
        await AssertCannotFinishAsync(rows, replacement, ct);

        await rows.DeleteBySubmissionAsync(key, ct);
        await AssertCannotFinishAsync(rows, old, ct);
        (await rows.GetBySubmissionIdAsync(TenantId, SubmissionId, ct)).Should().BeNull();
        await rows.EnsureExistsAsync(key, ct);
        var recreated = await ClaimAsync(rows, WriteOf(3), ct);
        await AssertCannotFinishAsync(rows, old, ct);
        (await rows.TryMarkProcessedAsync(recreated, ProcessedData, ct)).Should().BeTrue();
    }

    private static async Task AssertCannotFinishAsync(FlattenedSubmissionRepository rows, FlattenedRevision write, CancellationToken ct)
    {
        (await rows.TryMarkProcessedAsync(write, ProcessedData, ct)).Should().BeFalse();
        (await rows.TryMarkSkippedAsync(write, ct)).Should().BeFalse();
        (await rows.TryMarkFailedAsync(write, "rejected", ct)).Should().BeFalse();
    }

    [Fact]
    public async Task MarkDeleted_TrackedBeforeClaim_RevokesTheConcurrentWriter()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(ct);
        await using var db = CreateContext(TenantId);
        var rows = CreateRepository(db);
        await rows.EnsureExistsAsync(KeyOf(SubmissionId), ct);
        await using var deleting = CreateContext(TenantId);
        var original = await deleting.FlattenedSubmissions.SingleAsync(ct);
        var claimed = await ClaimAsync(rows, WriteOf(3), ct);

        // Act
        original.MarkDeleted();
        await deleting.SaveChangesAsync(ct);

        // Assert
        await AssertCannotFinishAsync(rows, claimed, ct);
        (await rows.GetBySubmissionIdAsync(TenantId, SubmissionId, ct)).Should().BeNull();
    }

    [Theory]
    [InlineData(Snapshot.Matching, SubmissionIntegrationStatusCodes.Processed)]
    [InlineData(Snapshot.TimestampBefore, SubmissionIntegrationStatusCodes.Processing)]
    [InlineData(Snapshot.TimestampAfter, SubmissionIntegrationStatusCodes.Processing)]
    [InlineData(Snapshot.NoAnswers, SubmissionIntegrationStatusCodes.Processing)]
    [InlineData(Snapshot.Deleted, SubmissionIntegrationStatusCodes.Processing)]
    [InlineData(Snapshot.Failed, SubmissionIntegrationStatusCodes.Processing)]
    [InlineData(Snapshot.OlderRevision, SubmissionIntegrationStatusCodes.Processing)]
    [InlineData(Snapshot.Forced, SubmissionIntegrationStatusCodes.Processing)]
    public async Task TryMarkProcessingAsync_OnlyMatchingLivePreparedReplay_RetainsProcessed(Snapshot snapshot, string expected)
    {
        // Arrange: change one condition at a time, including invalid historical rows with missing answers.
        var ct = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(ct);
        await using var db = CreateContext(TenantId);
        var rows = CreateRepository(db);
        var stored = WriteOf(3);
        await rows.EnsureExistsAsync(KeyOf(SubmissionId), ct);
        (await rows.TryMarkProcessedAsync(await ClaimAsync(rows, stored, ct), ProcessedData, ct)).Should().BeTrue();
        var all = db.FlattenedSubmissions.IgnoreQueryFilters().Where(row => row.SubmissionId == SubmissionId);
        await SetSnapshotStateAsync(all, snapshot, ct);

        // Act
        var write = AttemptFor(stored, snapshot);
        var landed = await rows.TryMarkProcessingAsync(write, ct);

        // Assert: bypass only the read filter to observe the transition even on the tombstone.
        landed.Should().BeTrue();
        var row = await all.AsNoTracking().SingleAsync(ct);
        row.Integration.Code.Should().Be(expected);
        row.SourceRevision.Should().Be(write.Revision);
        row.IsDeleted.Should().Be(snapshot == Snapshot.Deleted);
    }

    private static async Task<FlattenedRevision> ClaimAsync(FlattenedSubmissionRepository rows, FlattenedRevision write, CancellationToken ct)
    {
        (await rows.TryMarkProcessingAsync(write, ct)).Should().BeTrue();
        return write;
    }

    private static FlattenedRevision AttemptFor(FlattenedRevision stored, Snapshot snapshot) => WriteOf(stored.Revision) with
    {
        KeepPrepared = snapshot != Snapshot.Forced,
        Revision = snapshot == Snapshot.OlderRevision ? stored.Revision + 1 : stored.Revision,
        SourceModifiedAt = snapshot switch
        {
            Snapshot.TimestampBefore => stored.SourceModifiedAt.AddSeconds(-1),
            Snapshot.TimestampAfter => stored.SourceModifiedAt.AddSeconds(1),
            _ => stored.SourceModifiedAt,
        },
    };

    private static Task SetSnapshotStateAsync(IQueryable<FlattenedSubmission> rows, Snapshot snapshot, CancellationToken ct) => snapshot switch
    {
        Snapshot.NoAnswers => rows.ExecuteUpdateAsync(set => set.SetProperty(row => row.DataJson, (string?)null), ct),
        Snapshot.Deleted => rows.ExecuteUpdateAsync(set => set.SetProperty(row => row.IsDeleted, true), ct),
        Snapshot.Failed => rows.ExecuteUpdateAsync(set => set.SetProperty(row => row.Integration.Code, SubmissionIntegrationStatusCodes.Failed), ct),
        _ => Task.CompletedTask,
    };
}
