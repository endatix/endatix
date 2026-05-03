using System.Linq.Expressions;
using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Specifications;

internal static class SubmissionSpecificationExtensions
{
    private const string STATUS_FIELD_NAME = "status";

    internal static ISpecificationBuilder<Submission> WhereFormIdAndFilters(
        this ISpecificationBuilder<Submission> query,
        long formId,
        FilterParameters filterParams)
    {
        query.Where(s => s.FormDefinition.FormId == formId);

        var statusFilters = filterParams.Criteria
            .Where(c => c.Field.Equals(STATUS_FIELD_NAME, StringComparison.OrdinalIgnoreCase));
        foreach (var statusFilter in statusFilters)
        {
            var statusCodes = statusFilter.Values.ToList();
            query = statusFilter.Operator switch
            {
                ExpressionType.Equal => query.Where(s => statusCodes.Contains(s.Status.Code)),
                ExpressionType.NotEqual => query.Where(s => !statusCodes.Contains(s.Status.Code)),
                _ => throw new NotSupportedException($"Operator {statusFilter.Operator} is not supported for status filters.")
            };
        }

        var nonStatusFilters = new FilterParameters();
        filterParams.Criteria
            .Where(c => !c.Field.Equals(STATUS_FIELD_NAME, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .ForEach(nonStatusFilters.AddFilter);

        return query.Filter(nonStatusFilters);
    }
}
