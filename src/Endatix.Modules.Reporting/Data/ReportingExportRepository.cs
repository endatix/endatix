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
        var probeOptions = options with { PageSize = 1, AfterSubmissionId = null };
        await using var enumerator =
            StreamFlattenedSubmissionsAsync(
                tenantId,
                formId,
                probeOptions,
                cancellationToken).GetAsyncEnumerator(cancellationToken);

        return await enumerator.MoveNextAsync();
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
            var batchQuery = BuildExportableRowsQuery(tenantId, formId, options);
            if (afterSubmissionId is not null)
            {
                batchQuery = batchQuery.Where(row => row.SubmissionId > afterSubmissionId);
            }

            var batch = await batchQuery
                .OrderBy(row => row.SubmissionId)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                yield break;
            }

            var submissionIds = batch.Select(row => row.SubmissionId).ToList();
            var submissionsQuery = appDbContext.Submissions
                .AsNoTracking()
                .Where(submission => submission.TenantId == tenantId &&
                                     submission.FormId == formId &&
                                     submissionIds.Contains(submission.Id));

            submissionsQuery = ApplyCoreSubmissionFilters(submissionsQuery, options);

            var submissions = await submissionsQuery
                .ToDictionaryAsync(submission => submission.Id, cancellationToken);

            foreach (var flattened in batch)
            {
                if (!submissions.TryGetValue(flattened.SubmissionId, out var submission))
                {
                    logger.LogWarning(
                        "Skipping flattened submission {SubmissionId} for tenant {TenantId} form {FormId}: core submission row not found or filtered out.",
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
        var query = reportingDbContext.FlattenedSubmissions
            .AsNoTracking()
            .Where(row => row.TenantId == tenantId &&
                          row.FormId == formId &&
                          !row.IsDeleted &&
                          row.Integration.Code == SubmissionIntegrationStatusCodes.Processed &&
                          row.DataJson != null);

        if (options.MinSubmissionId is long minSubmissionId)
        {
            query = query.Where(row => row.SubmissionId >= minSubmissionId);
        }

        if (options.MaxSubmissionId is long maxSubmissionId)
        {
            query = query.Where(row => row.SubmissionId <= maxSubmissionId);
        }

        return query;
    }

    private static IQueryable<Endatix.Core.Entities.Submission> ApplyCoreSubmissionFilters(
        IQueryable<Endatix.Core.Entities.Submission> query,
        ExportQueryOptions options)
    {
        if (!options.IncludeTestSubmissions)
        {
            query = query.Where(submission => !submission.IsTestSubmission);
        }

        if (options.CreatedAfter is DateTime createdAfter)
        {
            query = query.Where(submission => submission.CreatedAt >= createdAfter);
        }

        if (options.CreatedBefore is DateTime createdBefore)
        {
            query = query.Where(submission => submission.CreatedAt < createdBefore);
        }

        if (options.StartedAfter is DateTime startedAfter)
        {
            query = query.Where(submission =>
                submission.StartedAt != null && submission.StartedAt >= startedAfter);
        }

        if (options.StartedBefore is DateTime startedBefore)
        {
            query = query.Where(submission =>
                submission.StartedAt != null && submission.StartedAt < startedBefore);
        }

        if (options.CompletedAfter is DateTime completedAfter)
        {
            query = query.Where(submission =>
                submission.CompletedAt != null && submission.CompletedAt >= completedAfter);
        }

        if (options.CompletedBefore is DateTime completedBefore)
        {
            query = query.Where(submission =>
                submission.CompletedAt != null && submission.CompletedAt < completedBefore);
        }

        if (options.IsComplete is bool isComplete)
        {
            query = query.Where(submission => submission.IsComplete == isComplete);
        }

        return query;
    }

    private static int NormalizePageSize(int pageSize) =>
        pageSize switch
        {
            <= 0 => DEFAULT_PAGE_SIZE,
            > MAX_PAGE_SIZE => MAX_PAGE_SIZE,
            _ => pageSize,
        };
}
