using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions;
using Endatix.Core.UseCases.Submissions.ListByFormId;
using Endatix.Modules.Reporting.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Reporting.Features.SubmissionGrid;

/// <summary>
/// Which page of a form's submissions to read, and which answer columns to return with it.
/// </summary>
/// <param name="TenantId">The caller's tenant.</param>
/// <param name="List">The same query the submissions list runs: filters, sort and paging.</param>
/// <param name="Columns">The answer columns to return per row.</param>
public sealed record SubmissionGridRowsQuery(long TenantId, ListByFormIdQuery List, IReadOnlyList<string> Columns);

/// <summary>
/// Reads one page of a form's submissions with their answers for the requested columns.
/// </summary>
/// <remarks>
/// The page comes from the core submissions list, so filters, sort and paging are the list's own. Prepared rows
/// are then read for that page only, by id. A query that filters on answers needs both in one statement; that is
/// endatix#1091.
/// </remarks>
public sealed class ListSubmissionGridRows(IMediator mediator, IReportingDbContext reportingDbContext)
{
    /// <summary>Reads the page and the answers of its rows.</summary>
    /// <param name="query">The tenant, the list query and the answer columns.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The page of rows, or the list's own failure; unauthorized without a tenant.</returns>
    public async Task<Result<Paged<SubmissionGridRow>>> ExecuteAsync(
        SubmissionGridRowsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.TenantId <= 0)
        {
            return Result.Unauthorized();
        }

        var page = await mediator.Send(query.List, cancellationToken);
        if (!page.IsSuccess)
        {
            return page.ToErrorResult<Paged<SubmissionGridRow>>();
        }

        var preparedRows = await ReadPreparedRowsAsync(query, page.Value.Items, cancellationToken);
        return Result.Success(ToGridPage(page.Value, preparedRows, query.Columns));
    }

    private static Paged<SubmissionGridRow> ToGridPage(
        Paged<SubmissionDto> page,
        Dictionary<long, PreparedRow> preparedRows,
        IReadOnlyList<string> columns)
    {
        var rows = page.Items
            .Select(submission => SubmissionGridRows.Compose(
                submission,
                preparedRows.GetValueOrDefault(submission.Id),
                columns))
            .ToList();

        return new Paged<SubmissionGridRow>(page.Page, page.PageSize, page.TotalRecords, page.TotalPages, rows);
    }

    private async Task<Dictionary<long, PreparedRow>> ReadPreparedRowsAsync(
        SubmissionGridRowsQuery query,
        IReadOnlyList<SubmissionDto> submissions,
        CancellationToken cancellationToken)
    {
        if (submissions.Count == 0)
        {
            return [];
        }

        var ids = submissions.Select(submission => submission.Id).ToList();
        return await PreparedRowsOf(query, ids).ToDictionaryAsync(row => row.SubmissionId, cancellationToken);
    }

    // The page's prepared rows. Answers are read only when columns were asked for; the state never needs them.
    private IQueryable<PreparedRow> PreparedRowsOf(SubmissionGridRowsQuery query, List<long> ids)
    {
        var readAnswers = query.Columns.Count > 0;
        return reportingDbContext.FlattenedSubmissions
            .AsNoTracking()
            .Where(row => row.TenantId == query.TenantId &&
                          row.FormId == query.List.FormId &&
                          !row.IsDeleted &&
                          ids.Contains(row.SubmissionId))
            .Select(row => new PreparedRow(
                row.SubmissionId,
                row.Integration.Code,
                row.DataJson != null,
                readAnswers ? row.DataJson : null,
                row.SourceModifiedAt));
    }
}
