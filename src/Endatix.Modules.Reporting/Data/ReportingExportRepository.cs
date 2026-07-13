using System.Runtime.CompilerServices;
using Endatix.Core.Entities;
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
        return await BuildExportableRowsQuery(tenantId, formId, options)
            .AnyAsync(cancellationToken);
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
            var batch = await BuildExportableRowsQuery(tenantId, formId, options)
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

    private IQueryable<FlattenedSubmission> BuildExportableRowsQuery(
        long tenantId,
        long formId,
        ExportQueryOptions options)
    {
        var flattenedRows = reportingDbContext.FlattenedSubmissions
            .AsNoTracking()
            .Where(row => row.TenantId == tenantId &&
                          row.FormId == formId &&
                          !row.IsDeleted &&
                          row.Integration.Code == SubmissionIntegrationStatusCodes.Processed &&
                          row.DataJson != null);

        if (options.IncludeTestSubmissions)
        {
            return flattenedRows;
        }

        var nonTestSubmissions = appDbContext.Submissions
            .AsNoTracking()
            .Where(submission => submission.TenantId == tenantId &&
                                 submission.FormId == formId &&
                                 !submission.IsTestSubmission);

        return from flattened in flattenedRows
               join submission in nonTestSubmissions on flattened.SubmissionId equals submission.Id
               select flattened;
    }

    private static int NormalizePageSize(int pageSize) =>
        pageSize switch
        {
            <= 0 => DEFAULT_PAGE_SIZE,
            > MAX_PAGE_SIZE => MAX_PAGE_SIZE,
            _ => pageSize,
        };
}
