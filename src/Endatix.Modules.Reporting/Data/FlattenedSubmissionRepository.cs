using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

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
        long submissionId,
        long tenantId,
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
}
