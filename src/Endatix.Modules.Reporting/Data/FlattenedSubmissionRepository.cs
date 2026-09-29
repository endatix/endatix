using Ardalis.GuardClauses;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Endatix.Modules.Reporting.Data;

/// <summary>
/// Repository for flattened submissions.
/// </summary>
internal sealed class FlattenedSubmissionRepository(
    ReportingDbContext dbContext,
    IReportingUnitOfWork unitOfWork) : IFlattenedSubmissionRepository
{
    /// <inheritdoc />
    public async Task<FlattenedSubmission?> GetBySubmissionIdAsync(
        long tenantId,
        long submissionId,
        CancellationToken cancellationToken)
    {
        return await dbContext.FlattenedSubmissions
            .Where(row => row.TenantId == tenantId && row.SubmissionId == submissionId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FlattenedSubmission> GetOrCreateAsync(
        long tenantId,
        long submissionId,
        long formId,
        CancellationToken cancellationToken)
    {
        var existing = await GetBySubmissionIdAsync(tenantId, submissionId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        FlattenedSubmission created = new(submissionId, tenantId, formId);
        await dbContext.FlattenedSubmissions.AddAsync(created, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return created;
    }

    /// <inheritdoc />
    public async Task SaveAsync(FlattenedSubmission flattenedSubmission, CancellationToken cancellationToken)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> DeleteByFormIdAsync(long tenantId, long formId, CancellationToken cancellationToken) =>
        dbContext.FlattenedSubmissions
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId && row.FormId == formId)
            .ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc />
    public Task<int> DeleteBySubmissionIdAsync(
        long tenantId,
        long submissionId,
        CancellationToken cancellationToken) =>
        dbContext.FlattenedSubmissions
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId && row.SubmissionId == submissionId)
            .ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc />
    public Task<int> DeleteBySubmissionAsync(FlattenedSubmissionKey key, CancellationToken cancellationToken) =>
        dbContext.FlattenedSubmissions
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == key.TenantId
                && row.FormId == key.FormId
                && row.SubmissionId == key.SubmissionId)
            .ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc />
    public async Task EnsureExistsAsync(FlattenedSubmissionKey key, CancellationToken cancellationToken)
    {
        if (!await ExistsAsync(key.SubmissionId, cancellationToken))
        {
            await InsertUnlessRacedAsync(new FlattenedSubmission(key.SubmissionId, key.TenantId, key.FormId), cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task<bool> TryMarkProcessingAsync(FlattenedRevision write, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return TryWriteAsync(
            write,
            setters => setters
                .SetProperty(row => row.Integration.Code, SubmissionIntegrationStatusCodes.Processing)
                .SetProperty(row => row.Integration.LastAttemptAt, now)
                .SetProperty(row => row.Integration.LastError, (string?)null)
                .SetProperty(row => row.ModifiedAt, now),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> TryMarkProcessedAsync(FlattenedRevision write, string dataJson, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(dataJson);

        var now = DateTime.UtcNow;
        return TryWriteAsync(
            write,
            setters => setters
                .SetProperty(row => row.DataJson, dataJson)
                .SetProperty(row => row.Integration.Code, SubmissionIntegrationStatusCodes.Processed)
                .SetProperty(row => row.Integration.LastAttemptAt, now)
                .SetProperty(row => row.Integration.ProcessedAt, now)
                .SetProperty(row => row.Integration.LastError, (string?)null)
                .SetProperty(row => row.IsDeleted, false)
                .SetProperty(row => row.SourceRevision, write.Revision)
                .SetProperty(row => row.ModifiedAt, now),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> TryMarkSkippedAsync(FlattenedRevision write, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return TryWriteAsync(
            write,
            setters => setters
                .SetProperty(row => row.DataJson, (string?)null)
                .SetProperty(row => row.Integration.Code, SubmissionIntegrationStatusCodes.Skipped)
                .SetProperty(row => row.Integration.LastAttemptAt, now)
                .SetProperty(row => row.Integration.ProcessedAt, (DateTime?)null)
                .SetProperty(row => row.Integration.LastError, (string?)null)
                .SetProperty(row => row.SourceRevision, write.Revision)
                .SetProperty(row => row.ModifiedAt, now),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> TryMarkFailedAsync(FlattenedRevision write, string error, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var storedError = SubmissionIntegrationState.TruncateError(error);
        return TryWriteAsync(
            write,
            setters => setters
                .SetProperty(row => row.Integration.Code, SubmissionIntegrationStatusCodes.Failed)
                .SetProperty(row => row.Integration.LastAttemptAt, now)
                .SetProperty(row => row.Integration.LastError, storedError)
                .SetProperty(row => row.ModifiedAt, now),
            cancellationToken);
    }

    private async Task InsertUnlessRacedAsync(FlattenedSubmission created, CancellationToken cancellationToken)
    {
        dbContext.FlattenedSubmissions.Add(created);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            ForgetTracked(created.SubmissionId);
        }
        catch (DbUpdateException)
        {
            // Two first flattens of one submission both found no row; the one that lost the insert uses the
            // other's row. Anything else is not a duplicate and fails as it would have.
            dbContext.Entry(created).State = EntityState.Detached;
            if (!await ExistsAsync(created.SubmissionId, cancellationToken))
            {
                throw;
            }
        }
    }

    private async Task<bool> TryWriteAsync(
        FlattenedRevision write,
        Action<UpdateSettersBuilder<FlattenedSubmission>> setters,
        CancellationToken cancellationToken) =>
        await NotNewerThan(write).ExecuteUpdateAsync(setters, cancellationToken) == 1
        && ForgetTracked(write.SubmissionId);

    // Set-based and conditional in one statement, so two flattens of one submission cannot interleave a read and a
    // write: whichever revision is older loses, whatever order they finish in.
    private IQueryable<FlattenedSubmission> NotNewerThan(FlattenedRevision write) =>
        dbContext.FlattenedSubmissions
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == write.TenantId
                && row.SubmissionId == write.SubmissionId
                && (row.SourceRevision == null || row.SourceRevision <= write.Revision));

    // The conditional writes bypass the change tracker, so a copy of the row this context tracks would go stale;
    // it is let go so the next read in the same scope sees the row as written. Returns true for use after a write.
    private bool ForgetTracked(long submissionId)
    {
        foreach (var entry in dbContext.ChangeTracker.Entries<FlattenedSubmission>()
                     .Where(entry => entry.Entity.SubmissionId == submissionId)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        return true;
    }

    private Task<bool> ExistsAsync(long submissionId, CancellationToken cancellationToken) =>
        dbContext.FlattenedSubmissions
            .IgnoreQueryFilters()
            .AnyAsync(row => row.SubmissionId == submissionId, cancellationToken);
}
