using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

/// <summary>
/// Writes flattened rows the way a flatten does: the row is created, then its state is set by the repository's
/// conditional writes.
/// </summary>
internal sealed class FlattenedRowSeed(ReportingDbContextBase dbContext)
{
    // Every seeded write uses one revision, and a write from the same revision as the row's still lands.
    private const long SeedRevision = 1;

    private readonly FlattenedSubmissionRepository _repository = new(dbContext, new ReportingUnitOfWork(dbContext));

    /// <summary>Creates a pending row and returns it tracked by the seed's context.</summary>
    public async Task<FlattenedSubmission> PendingAsync(FlattenedSubmissionKey key, CancellationToken cancellationToken)
    {
        await _repository.EnsureExistsAsync(key, cancellationToken);
        return await dbContext.FlattenedSubmissions
            .SingleAsync(row => row.SubmissionId == key.SubmissionId, cancellationToken);
    }

    /// <summary>Creates a row and marks it deleted, as a flatten does for a submission deleted before it ran.</summary>
    public async Task SoftDeletedAsync(FlattenedSubmissionKey key, CancellationToken cancellationToken)
    {
        var row = await PendingAsync(key, cancellationToken);
        row.MarkDeleted();
        await _repository.SaveAsync(row, cancellationToken);
    }

    /// <summary>Creates a row processed with <paramref name="dataJson"/>.</summary>
    public async Task ProcessedAsync(FlattenedSubmissionKey key, string dataJson, CancellationToken cancellationToken)
    {
        await _repository.EnsureExistsAsync(key, cancellationToken);
        var landed = await _repository.TryMarkProcessedAsync(RevisionOf(key), dataJson, cancellationToken);
        landed.Should().BeTrue("the seeded row has no newer revision");
    }

    /// <summary>Marks an existing row failed with <paramref name="error"/>.</summary>
    public async Task FailedAsync(FlattenedSubmissionKey key, string error, CancellationToken cancellationToken)
    {
        var landed = await _repository.TryMarkFailedAsync(RevisionOf(key), error, cancellationToken);
        landed.Should().BeTrue("the seeded row has no newer revision");
    }

    private static FlattenedRevision RevisionOf(FlattenedSubmissionKey key) =>
        new(key.TenantId, key.SubmissionId, SeedRevision, DateTime.UtcNow);
}
