using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

public sealed partial class FlattenedSubmissionRepositoryTests
{
    public enum Snapshot { Matching, TimestampBefore, TimestampAfter, NoAnswers, Deleted, Failed, OlderRevision, Forced }

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
        (await rows.TryMarkProcessedAsync(stored, ProcessedData, ct)).Should().BeTrue();
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

    private static FlattenedRevision AttemptFor(FlattenedRevision stored, Snapshot snapshot) => stored with
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
