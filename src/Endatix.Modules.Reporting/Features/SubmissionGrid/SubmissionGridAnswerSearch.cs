using Ardalis.Specification.EntityFrameworkCore;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;
using Endatix.Infrastructure.Data.Querying;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using PreparedSubmission = Endatix.Modules.Reporting.Domain.FlattenedSubmission;

namespace Endatix.Modules.Reporting.Features.SubmissionGrid;

/// <summary>
/// The grid's submissions list with answer filters: the submissions that pass the list's metadata filters and whose
/// prepared answers match every answer filter, and a count of those that could not be checked.
/// </summary>
/// <remarks>
/// <para>
/// Answers are checked only on rows prepared from the submission's current version, through the form's search copy
/// (<see cref="PreparedSubmission.SearchJson"/>). Every other submission inside the metadata filters is counted in
/// <see cref="AnswerFilterCoverage"/> instead of being dropped without a word.
/// </para>
/// <para>
/// The matches are read once, as ordered ids: the count and the page both come from that list, so the filters scan
/// the rows once. The page's rows are then read by id.
/// </para>
/// </remarks>
public sealed class SubmissionGridAnswerSearch(
    IReportingDbContext reportingDbContext,
    IRelationalJsonObjectKeyFilter keyFilter,
    IRelationalSubmitterProfileFilter profileFilter,
    IRepository<Form> formsRepository)
{
    internal const string NotPreparedErrorCode = "reporting_not_prepared";
    internal const string NotPreparedMessage = "The form has no prepared reporting schema, so its answers cannot be filtered.";

    private const string SearchJsonProperty = nameof(PreparedSubmission.SearchJson);

    internal async Task<Result<SubmissionGridPage>> ExecuteAsync(
        SubmissionGridRowsQuery query,
        CancellationToken cancellationToken)
    {
        var searchKeys = await ReadSearchKeysAsync(query, cancellationToken);
        if (searchKeys is null)
        {
            return await formsRepository.IsTenantFormAsync(query.TenantId, query.List.FormId, cancellationToken)
                ? Result.Conflict(NotPreparedMessage)
                : Result.NotFound(TenantFormExtensions.FormNotFoundMessage);
        }

        var invalid = AnswerFilterRules.Check(query.Answers, searchKeys);
        return invalid.Count > 0
            ? Result.Invalid(invalid)
            : Result.Success(await SearchAsync(query, ScopeOf(query, searchKeys), cancellationToken));
    }

    private async Task<SubmissionGridPage> SearchAsync(
        SubmissionGridRowsQuery query,
        SearchScope scope,
        CancellationToken cancellationToken)
    {
        var ids = await MatchingIdsAsync(query, scope, cancellationToken);
        var resolved = new PagingParameters(query.List.Page, query.List.PageSize).ForTotal(ids.Count);
        var pageIds = ids.Skip(resolved.Skip).Take(resolved.PageSize).ToList();

        var rows = await ReadPageAsync(query, pageIds, cancellationToken);
        var coverage = await CountUncheckedAsync(scope, cancellationToken);
        return new SubmissionGridPage(resolved.ToPaged(rows), coverage);
    }

    // A row is checked only when its search copy holds every filtered key: a key added since the row was written
    // waits for the rebuild, and until then the row counts as not checked.
    private SearchScope ScopeOf(SubmissionGridRowsQuery query, FormSearchKeys searchKeys)
    {
        var revisionNeeded = searchKeys.RevisionNeededFor(query.Answers.Select(answer => answer.Key));
        return new SearchScope(Listed(query), Searchable(query).HoldingSearchKeys(revisionNeeded));
    }

    private async Task<FormSearchKeys?> ReadSearchKeysAsync(SubmissionGridRowsQuery query, CancellationToken cancellationToken)
    {
        var schema = await reportingDbContext.FormSchemas
            .AsNoTracking()
            .Where(schema => schema.TenantId == query.TenantId && schema.FormId == query.List.FormId)
            .Select(schema => new { schema.SearchKeys })
            .SingleOrDefaultAsync(cancellationToken);
        return schema is null ? null : FormSearchKeys.FromJson(schema.SearchKeys);
    }

    private IQueryable<CoreSubmissionRow> Listed(SubmissionGridRowsQuery query)
    {
        FilterParameters filters = new(query.List.FilterExpressions ?? []);
        var submissions = reportingDbContext.CoreSubmissionRows
            .Where(submission => submission.TenantId == query.TenantId && !submission.IsDeleted);
        var rows = SpecificationEvaluator.Default.GetQuery(submissions, new SubmissionGridMetadataSpec(query.List, filters));
        foreach (var filter in SubmissionFilterFields.SelectSubmitterProfileFilters(filters.Criteria))
        {
            rows = profileFilter.Where(rows, submission => submission.SubmitterProfileSnapshot, filter);
        }

        return rows;
    }

    // A row is checked only when it was prepared from the submission's current version;
    // the join to the submission decides that.
    private IQueryable<PreparedSubmission> Searchable(SubmissionGridRowsQuery query) =>
        reportingDbContext.FlattenedSubmissions
            .Where(row => row.TenantId == query.TenantId && row.FormId == query.List.FormId && !row.IsDeleted &&
                          row.Integration.Code == SubmissionIntegrationStatusCodes.Processed &&
                          row.SearchJson != null);

    private async Task<List<long>> MatchingIdsAsync(
        SubmissionGridRowsQuery query,
        SearchScope scope,
        CancellationToken cancellationToken)
    {
        var matched = query.Answers.Aggregate(scope.Searchable, WhereAnswerMatches);
        var matching = scope.Listed
            .Join(matched, submission => submission.Id, row => row.SubmissionId, (submission, row) => new { submission, row.SourceModifiedAt })
            .Where(pair => pair.SourceModifiedAt == (pair.submission.ModifiedAt ?? pair.submission.CreatedAt))
            .Select(pair => pair.submission);

        return await SubmissionGridMetadataSpec.OrderAsList(matching, query.List)
            .Select(submission => submission.Id)
            .ToListAsync(cancellationToken);
    }

    // The search copy holds text lowercased and dates in ISO form (SearchValues), so contains ignores case and a
    // day bound compares as text: "to" keeps everything before the next day, times included.
    private IQueryable<PreparedSubmission> WhereAnswerMatches(IQueryable<PreparedSubmission> rows, AnswerFilter filter)
    {
        JsonObjectKeyPath path = new(SearchJsonProperty, filter.Key);
        return filter.Operator switch
        {
            AnswerFilterOperators.Contains => keyFilter.WhereKeyMatches(rows, SearchJsonProperty, filter.Key, filter.Value),
            AnswerFilterOperators.From => keyFilter.WhereKeyAtLeast(rows, path, filter.Value),
            _ => keyFilter.WhereKeyBefore(rows, path, AnswerFilterExpressions.NextDay(filter.Value)),
        };
    }

    private static async Task<AnswerFilterCoverage> CountUncheckedAsync(
        SearchScope scope,
        CancellationToken cancellationToken)
    {
        var searchable = scope.Searchable;
        var counts = await scope.Listed
            .Where(submission => !searchable.Any(row =>
                row.SubmissionId == submission.Id &&
                row.SourceModifiedAt == (submission.ModifiedAt ?? submission.CreatedAt)))
            .GroupBy(submission => submission.IsComplete)
            .Select(group => new { IsComplete = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return new AnswerFilterCoverage(
            UncheckedCompleted: counts.Where(count => count.IsComplete).Sum(count => count.Count),
            UncheckedInProgress: counts.Where(count => !count.IsComplete).Sum(count => count.Count));
    }

    private async Task<IReadOnlyList<SubmissionGridRow>> ReadPageAsync(
        SubmissionGridRowsQuery query,
        List<long> pageIds,
        CancellationToken cancellationToken)
    {
        if (pageIds.Count == 0)
        {
            return [];
        }

        var submissions = await ReadSubmissionsAsync(query, pageIds, cancellationToken);
        var prepared = await ReadPreparedRowsAsync(query, pageIds, cancellationToken);
        return pageIds
            .Where(submissions.ContainsKey)
            .Select(id => SubmissionGridRows.Compose(
                CoreSubmissionRows.ToDto(submissions[id]),
                prepared.GetValueOrDefault(id),
                query.Columns))
            .ToList();
    }

    private Task<Dictionary<long, CoreSubmissionRow>> ReadSubmissionsAsync(
        SubmissionGridRowsQuery query,
        List<long> pageIds,
        CancellationToken cancellationToken) =>
        reportingDbContext.CoreSubmissionRows
            .AsNoTracking()
            .Where(submission => submission.TenantId == query.TenantId && pageIds.Contains(submission.Id))
            .ToDictionaryAsync(submission => submission.Id, cancellationToken);

    // Answers are read only when columns were asked for; the row state never needs them.
    private Task<Dictionary<long, PreparedRow>> ReadPreparedRowsAsync(
        SubmissionGridRowsQuery query,
        List<long> pageIds,
        CancellationToken cancellationToken)
    {
        var readAnswers = query.Columns.Count > 0;
        return reportingDbContext.FlattenedSubmissions
            .AsNoTracking()
            .Where(row => row.TenantId == query.TenantId && row.FormId == query.List.FormId && !row.IsDeleted &&
                          pageIds.Contains(row.SubmissionId))
            .Select(row => new PreparedRow(
                row.SubmissionId,
                row.Integration.Code,
                row.DataJson != null,
                readAnswers ? row.DataJson : null,
                row.SourceModifiedAt))
            .ToDictionaryAsync(row => row.SubmissionId, cancellationToken);
    }

    private sealed record SearchScope(IQueryable<CoreSubmissionRow> Listed, IQueryable<PreparedSubmission> Searchable);
}
