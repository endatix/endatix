using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.UseCases.Submissions.ListByFormId;
using System.Linq.Expressions;
using Endatix.Modules.Reporting.Persistence;

namespace Endatix.Modules.Reporting.Features.SubmissionGrid;

/// <summary>
/// The submissions list's metadata filters on <see cref="CoreSubmissionRow"/>: form, status, collection status,
/// the field filters and the date ranges, with the same rules as the core <c>SubmissionsByFormIdSpec</c>.
/// </summary>
/// <remarks>
/// Submitter profile filters are provider SQL and are applied by <see cref="SubmissionGridAnswerSearch"/>. Core also
/// requires the submission's form definition to be live (<c>s.FormDefinition.FormId</c>); this spec reads only
/// <see cref="CoreSubmissionRow.FormId"/>.
/// </remarks>
internal sealed class SubmissionGridMetadataSpec : Specification<CoreSubmissionRow>
{
    private const string StatusField = "status";
    private const string CollectionStatusField = "collectionStatus";

    public SubmissionGridMetadataSpec(ListByFormIdQuery list, FilterParameters filters)
    {
        Query.Where(row => row.FormId == list.FormId);
        WhereStatus(filters);
        WhereCollectionStatus(filters);
        Query.Filter(FieldFilters(filters));

        Query.WhereUtcRange(row => row.CreatedAt, list.Created);
        Query.WhereUtcRange(row => row.ModifiedAt, list.Modified);
        Query.WhereUtcRange(row => row.StartedAt, list.Started);
        Query.WhereUtcRange(row => row.CompletedAt, list.Completed);
    }

    /// <summary>
    /// Orders <paramref name="rows"/> as the core list does: newest first when no sort is given, otherwise by the sort
    /// field with the id breaking ties.
    /// </summary>
    public static IOrderedQueryable<CoreSubmissionRow> OrderAsList(
        IQueryable<CoreSubmissionRow> rows,
        ListByFormIdQuery list)
    {
        if (list.SortBy is null)
        {
            return rows.OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.CompletedAt);
        }

        return list.SortBy.Value switch
        {
            SubmissionListSortBy.Id when list.SortDescending => rows.OrderByDescending(row => row.Id),
            SubmissionListSortBy.Id => rows.OrderBy(row => row.Id),
            SubmissionListSortBy.ModifiedAt => WithIdTiebreaker(rows, row => row.ModifiedAt, list.SortDescending),
            SubmissionListSortBy.StartedAt => WithIdTiebreaker(rows, row => row.StartedAt, list.SortDescending),
            SubmissionListSortBy.CompletedAt => WithIdTiebreaker(rows, row => row.CompletedAt, list.SortDescending),
            _ => WithIdTiebreaker(rows, row => (DateTime?)row.CreatedAt, list.SortDescending),
        };
    }

    private static IOrderedQueryable<CoreSubmissionRow> WithIdTiebreaker(
        IQueryable<CoreSubmissionRow> rows,
        Expression<Func<CoreSubmissionRow, DateTime?>> key,
        bool descending) =>
        (descending ? rows.OrderByDescending(key) : rows.OrderBy(key)).ThenBy(row => row.Id);

    private void WhereStatus(FilterParameters filters)
    {
        foreach (var filter in FiltersFor(filters, StatusField))
        {
            var codes = filter.Values.ToList();
            Query.Where(filter.Operator switch
            {
                ExpressionType.Equal => row => codes.Contains(row.Status),
                ExpressionType.NotEqual => row => !codes.Contains(row.Status),
                _ => throw new NotSupportedException($"Operator {filter.Operator} is not supported for status filters.")
            });
        }
    }

    private void WhereCollectionStatus(FilterParameters filters)
    {
        foreach (var filter in FiltersFor(filters, CollectionStatusField))
        {
            // As in the core list: over-long codes can never be stored, so they match nothing.
            var codes = filter.Values
                .Where(code => code.Trim().Length <= CollectionStatus.CODE_MAX_LENGTH)
                .Select(code => CollectionStatus.FromCode(code).Code)
                .ToList();
            Query.Where(filter.Operator switch
            {
                ExpressionType.Equal => row => codes.Contains(row.CollectionStatus),
                ExpressionType.NotEqual => row => !codes.Contains(row.CollectionStatus),
                _ => throw new NotSupportedException($"Operator {filter.Operator} is not supported for collectionStatus filters.")
            });
        }
    }

    private static FilterParameters FieldFilters(FilterParameters filters)
    {
        FilterParameters fieldFilters = new();
        foreach (var filter in filters.Criteria.Where(IsFieldFilter))
        {
            fieldFilters.AddFilter(filter);
        }

        return fieldFilters;
    }

    private static bool IsFieldFilter(FilterCriterion filter) =>
        !IsField(filter, StatusField) &&
        !IsField(filter, CollectionStatusField) &&
        !SubmissionFilterFields.IsSubmitterProfileField(filter.Field);

    private static IEnumerable<FilterCriterion> FiltersFor(FilterParameters filters, string field) =>
        filters.Criteria.Where(filter => IsField(filter, field));

    private static bool IsField(FilterCriterion filter, string field) =>
        filter.Field.Equals(field, StringComparison.OrdinalIgnoreCase);
}
