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

        var statusFilter = filterParams.Criteria
            .FirstOrDefault(c => c.Field.Equals(STATUS_FIELD_NAME, StringComparison.OrdinalIgnoreCase));
        if (statusFilter != null && statusFilter.Values.Any())
        {
            var statusCodes = statusFilter.Values.ToList();
            query.Where(s => statusCodes.Contains(s.Status.Code));
        }

        var nonStatusFilters = new FilterParameters();
        filterParams.Criteria
            .Where(c => !c.Field.Equals(STATUS_FIELD_NAME, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .ForEach(nonStatusFilters.AddFilter);

        return query.Filter(nonStatusFilters);
    }
}
