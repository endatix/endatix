using System.Runtime.CompilerServices;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Reporting.Data;

/// <summary>
/// Repository for exporting reporting data.
/// </summary>
internal sealed class ReportingExportRepository(
    ReportingDbContext reportingDbContext,
    AppDbContext appDbContext,
    ILogger<ReportingExportRepository> logger) : IReportingExportRepository
{
    private const int DEFAULT_PAGE_SIZE = 500;
    private const int MAX_PAGE_SIZE = 5_000;

    public async Task<bool> HasExportableRowsAsync(
        long tenantId,
        long formId,
        ExportQueryOptions options,
        CancellationToken cancellationToken)
    {
        var flattenedRows = BuildExportableRowsQuery(tenantId, formId);

        if (options.IncludeTestSubmissions)
        {
            return await flattenedRows.AnyAsync(cancellationToken);
        }

        // Probe flattened rows in bounded batches and require a non-test core submission
        // for at least one ID. Avoids materializing all IDs or cross-DbContext joins.
        long? afterSubmissionId = null;
        while (true)
        {
            var batchIds = await flattenedRows
                .Where(row => afterSubmissionId == null || row.SubmissionId > afterSubmissionId)
                .OrderBy(row => row.SubmissionId)
                .Take(DEFAULT_PAGE_SIZE)
                .Select(row => row.SubmissionId)
                .ToListAsync(cancellationToken);

            if (batchIds.Count == 0)
            {
                return false;
            }

            var hasExportableRow = await appDbContext.Submissions
                .AsNoTracking()
                .AnyAsync(
                    submission => submission.TenantId == tenantId &&
                                  submission.FormId == formId &&
                                  !submission.IsTestSubmission &&
                                  batchIds.Contains(submission.Id),
                    cancellationToken);
            if (hasExportableRow)
            {
                return true;
            }

            if (batchIds.Count < DEFAULT_PAGE_SIZE)
            {
                return false;
            }

            afterSubmissionId = batchIds[^1];
        }
    }

    public async IAsyncEnumerable<FlattenedExportRow> StreamFlattenedSubmissionsAsync(
        long tenantId,
        long formId,
        ExportQueryOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var pageSize = NormalizePageSize(options.PageSize);
        var afterSubmissionId = options.AfterSubmissionId;

        while (true)
        {
            var batch = await BuildExportableRowsQuery(tenantId, formId)
                .Where(row => afterSubmissionId == null || row.SubmissionId > afterSubmissionId)
                .OrderBy(row => row.SubmissionId)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                yield break;
            }

            var submissionIds = batch.Select(row => row.SubmissionId).ToList();
            var submissions = await appDbContext.Submissions
                .AsNoTracking()
                .Where(submission => submission.TenantId == tenantId &&
                                     submission.FormId == formId &&
                                     (options.IncludeTestSubmissions || !submission.IsTestSubmission) &&
                                     submissionIds.Contains(submission.Id))
                .ToDictionaryAsync(submission => submission.Id, cancellationToken);

            foreach (var flattened in batch)
            {
                if (!submissions.TryGetValue(flattened.SubmissionId, out var submission))
                {
                    logger.LogWarning(
                        "Skipping flattened submission {SubmissionId} for tenant {TenantId} form {FormId}: core submission row not found.",
                        flattened.SubmissionId,
                        tenantId,
                        formId);
                    continue;
                }

                yield return new FlattenedExportRow(
                    SubmissionId: submission.Id,
                    FormId: submission.FormId,
                    IsComplete: submission.IsComplete,
                    CreatedAt: submission.CreatedAt,
                    ModifiedAt: submission.ModifiedAt,
                    CompletedAt: submission.CompletedAt,
                    SubmitterId: submission.SubmitterId,
                    SubmitterDisplayId: submission.SubmitterDisplayId,
                    DataJson: flattened.DataJson!);
            }

            if (batch.Count < pageSize)
            {
                yield break;
            }

            afterSubmissionId = batch[^1].SubmissionId;
        }
    }

    private IQueryable<FlattenedSubmission> BuildExportableRowsQuery(long tenantId, long formId)
    {
        return reportingDbContext.FlattenedSubmissions
            .AsNoTracking()
            .Where(row => row.TenantId == tenantId &&
                          row.FormId == formId &&
                          !row.IsDeleted &&
                          row.Integration.Code == SubmissionIntegrationStatusCodes.Processed &&
                          row.DataJson != null);
    }

    private static int NormalizePageSize(int pageSize) =>
        pageSize switch
        {
            <= 0 => DEFAULT_PAGE_SIZE,
            > MAX_PAGE_SIZE => MAX_PAGE_SIZE,
            _ => pageSize,
        };
}
