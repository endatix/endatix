using System.Linq.Expressions;
using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.UseCases.Submissions.ListByFormId;

namespace Endatix.Core.Specifications;

internal static class SubmissionSpecificationExtensions
{
    private const string STATUS_FIELD_NAME = "status";
    private const string COLLECTION_STATUS_FIELD_NAME = "collectionStatus";

    internal static ISpecificationBuilder<Submission> WhereFormIdAndFilters(
        this ISpecificationBuilder<Submission> query,
        long formId,
        FilterParameters filterParams)
    {
        query.Where(s => s.FormDefinition.FormId == formId && s.FormId == formId);

        query = ApplyStatusFilter(query, filterParams);
        query = ApplyCollectionStatusFilter(query, filterParams);

        var nonStatusFilters = new FilterParameters();
        filterParams.Criteria
            .Where(c =>
                !c.Field.Equals(STATUS_FIELD_NAME, StringComparison.OrdinalIgnoreCase) &&
                !c.Field.Equals(COLLECTION_STATUS_FIELD_NAME, StringComparison.OrdinalIgnoreCase) &&
                !SubmissionFilterFields.IsSubmitterProfileField(c.Field))
            .ToList()
            .ForEach(nonStatusFilters.AddFilter);

        return query.Filter(nonStatusFilters);
    }

    private static ISpecificationBuilder<Submission> ApplyStatusFilter(
        ISpecificationBuilder<Submission> query,
        FilterParameters filterParams)
    {
        foreach (var statusFilter in FiltersFor(filterParams, STATUS_FIELD_NAME))
        {
            var codes = statusFilter.Values.ToList();
            query = statusFilter.Operator switch
            {
                ExpressionType.Equal => query.Where(s => codes.Contains(s.Status.Code)),
                ExpressionType.NotEqual => query.Where(s => !codes.Contains(s.Status.Code)),
                _ => throw new NotSupportedException(
                    $"Operator {statusFilter.Operator} is not supported for status filters.")
            };
        }

        return query;
    }

    private static ISpecificationBuilder<Submission> ApplyCollectionStatusFilter(
        ISpecificationBuilder<Submission> query,
        FilterParameters filterParams)
    {
        foreach (var statusFilter in FiltersFor(filterParams, COLLECTION_STATUS_FIELD_NAME))
        {
            // Over-long codes can never be stored, so they match nothing instead of throwing (500).
            var statuses = statusFilter.Values
                .Where(code => code.Trim().Length <= CollectionStatus.CODE_MAX_LENGTH)
                .Select(CollectionStatus.FromCode)
                .ToList();
            query = statusFilter.Operator switch
            {
                ExpressionType.Equal => query.Where(s => statuses.Contains(s.CollectionStatus)),
                ExpressionType.NotEqual => query.Where(s => !statuses.Contains(s.CollectionStatus)),
                _ => throw new NotSupportedException(
                    $"Operator {statusFilter.Operator} is not supported for collectionStatus filters.")
            };
        }

        return query;
    }

    private static IEnumerable<FilterCriterion> FiltersFor(
        FilterParameters filterParams,
        string fieldName) =>
        filterParams.Criteria.Where(c =>
            c.Field.Equals(fieldName, StringComparison.OrdinalIgnoreCase));

    internal static ISpecificationBuilder<Submission> ApplyListDateRanges(
        this ISpecificationBuilder<Submission> query,
        SubmissionsListFilter listFilter)
    {
        query.WhereUtcRange(x => x.CreatedAt, listFilter.Created);
        query.WhereUtcRange(x => x.ModifiedAt, listFilter.Modified);
        query.WhereUtcRange(x => x.StartedAt, listFilter.Started);
        query.WhereUtcRange(x => x.CompletedAt, listFilter.Completed);
        return query;
    }
}
