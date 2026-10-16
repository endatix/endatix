using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Reporting.Features.SubmissionGrid;

/// <summary>
/// Which form's grid columns to read, and in which locale.
/// </summary>
public sealed record SubmissionGridColumnsQuery(long TenantId, long FormId, string Locale);

/// <summary>
/// Reads a form's submissions-grid columns from its compiled schema, with the form's preparation counts.
/// </summary>
public sealed class ListSubmissionGridColumns(
    IFormSchemaRepository formSchemaRepository,
    IReportingDbContext reportingDbContext,
    AppDbContext appDbContext)
{
    internal const string FormNotFoundMessage = "Form not found.";

    /// <summary>
    /// Returns the columns, or not found when the form does not exist in the tenant. A form without a compiled schema
    /// returns no columns and <see cref="SubmissionGridColumns.SchemaReady"/> <see langword="false"/>.
    /// </summary>
    public async Task<Result<SubmissionGridColumns>> ExecuteAsync(
        SubmissionGridColumnsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.TenantId <= 0)
        {
            return Result.Unauthorized();
        }

        if (!await FormExistsAsync(query, cancellationToken))
        {
            return Result.NotFound(FormNotFoundMessage);
        }

        var schema = await formSchemaRepository.GetByFormIdAsync(query.TenantId, query.FormId, cancellationToken);
        var readiness = await ReadReadinessAsync(query, cancellationToken);
        var columns = schema is null ? [] : SubmissionGridColumnCatalog.Build(schema, query.Locale);

        return Result.Success(new SubmissionGridColumns(query.FormId, schema is not null, readiness, columns));
    }

    private Task<bool> FormExistsAsync(SubmissionGridColumnsQuery query, CancellationToken cancellationToken) =>
        appDbContext.Forms
            .AsNoTracking()
            .AnyAsync(form => form.TenantId == query.TenantId && form.Id == query.FormId, cancellationToken);

    private async Task<SubmissionGridReadiness> ReadReadinessAsync(
        SubmissionGridColumnsQuery query,
        CancellationToken cancellationToken)
    {
        var submissions = await CountSubmissionsAsync(query, cancellationToken);
        var rowsByStatus = await CountPreparedRowsByStatusAsync(query, cancellationToken);

        return new SubmissionGridReadiness(
            Submissions: submissions.Total,
            CompletedSubmissions: submissions.Completed,
            Prepared: rowsByStatus.GetValueOrDefault(SubmissionIntegrationStatusCodes.Processed),
            Pending: rowsByStatus.GetValueOrDefault(SubmissionIntegrationStatusCodes.Pending) +
                     rowsByStatus.GetValueOrDefault(SubmissionIntegrationStatusCodes.Processing),
            Failed: rowsByStatus.GetValueOrDefault(SubmissionIntegrationStatusCodes.Failed),
            SkippedInProgress: rowsByStatus.GetValueOrDefault(SubmissionIntegrationStatusCodes.Skipped));
    }

    private async Task<SubmissionCounts> CountSubmissionsAsync(
        SubmissionGridColumnsQuery query,
        CancellationToken cancellationToken) =>
        await appDbContext.Submissions
            .AsNoTracking()
            .Where(submission => submission.TenantId == query.TenantId &&
                                 submission.FormId == query.FormId &&
                                 !submission.IsDeleted)
            .GroupBy(_ => 1)
            .Select(group => new SubmissionCounts(group.Count(), group.Count(submission => submission.IsComplete)))
            .FirstOrDefaultAsync(cancellationToken) ?? new SubmissionCounts(0, 0);

    private Task<Dictionary<string, int>> CountPreparedRowsByStatusAsync(
        SubmissionGridColumnsQuery query,
        CancellationToken cancellationToken) =>
        reportingDbContext.FlattenedSubmissions
            .AsNoTracking()
            .Where(row => row.TenantId == query.TenantId && row.FormId == query.FormId && !row.IsDeleted)
            .GroupBy(row => row.Integration.Code)
            .Select(group => new { Code = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.Code, entry => entry.Count, cancellationToken);

    private sealed record SubmissionCounts(int Total, int Completed);
}
