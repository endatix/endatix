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

    public Task<bool> HasCompletedSubmissionsAsync(
        long tenantId,
        long formId,
        CancellationToken cancellationToken) =>
        appDbContext.Submissions
            .AsNoTracking()
            .AnyAsync(
                submission => submission.TenantId == tenantId &&
                              submission.FormId == formId &&
                              submission.IsComplete,
                cancellationToken);

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
                    StartedAt: submission.StartedAt,
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

        if (options.CreatedFrom is DateTime createdFrom)
        {
            query = query.Where(submission => submission.CreatedAt >= createdFrom);
        }

        if (options.CreatedTo is DateTime createdTo)
        {
            // The 9999-12-31 calendar day clamps to DateTime.MaxValue, which has no exclusive
            // successor - compare inclusively so a row stamped at the sentinel isn't dropped.
            query = createdTo == DateTime.MaxValue
                ? query.Where(submission => submission.CreatedAt <= createdTo)
                : query.Where(submission => submission.CreatedAt < createdTo);
        }

        if (options.ModifiedFrom is DateTime modifiedFrom)
        {
            query = query.Where(submission =>
                submission.ModifiedAt != null && submission.ModifiedAt >= modifiedFrom);
        }

        if (options.ModifiedTo is DateTime modifiedTo)
        {
            query = modifiedTo == DateTime.MaxValue
                ? query.Where(submission =>
                    submission.ModifiedAt != null && submission.ModifiedAt <= modifiedTo)
                : query.Where(submission =>
                    submission.ModifiedAt != null && submission.ModifiedAt < modifiedTo);
        }

        if (options.StartedFrom is DateTime startedFrom)
        {
            query = query.Where(submission =>
                submission.StartedAt != null && submission.StartedAt >= startedFrom);
        }

        if (options.StartedTo is DateTime startedTo)
        {
            query = startedTo == DateTime.MaxValue
                ? query.Where(submission =>
                    submission.StartedAt != null && submission.StartedAt <= startedTo)
                : query.Where(submission =>
                    submission.StartedAt != null && submission.StartedAt < startedTo);
        }

        if (options.CompletedFrom is DateTime completedFrom)
        {
            query = query.Where(submission =>
                submission.CompletedAt != null && submission.CompletedAt >= completedFrom);
        }

        if (options.CompletedTo is DateTime completedTo)
        {
            query = completedTo == DateTime.MaxValue
                ? query.Where(submission =>
                    submission.CompletedAt != null && submission.CompletedAt <= completedTo)
                : query.Where(submission =>
                    submission.CompletedAt != null && submission.CompletedAt < completedTo);
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
