using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Specifications;

public class SubmissionsByFormIdCountSpec : Specification<Submission>
{
    private const string STATUS_FIELD_NAME = "status";

    public SubmissionsByFormIdCountSpec(long formId, FilterParameters filterParams)
    {
        Query.Where(s => s.FormDefinition.FormId == formId);

        var statusFilter = filterParams.Criteria
            .FirstOrDefault(c => c.Field.Equals(STATUS_FIELD_NAME, StringComparison.OrdinalIgnoreCase));
        if (statusFilter != null && statusFilter.Values.Any())
        {
            var statusCodes = statusFilter.Values.ToList();
            Query.Where(s => statusCodes.Contains(s.Status.Code));
        }

        var nonStatusFilters = new FilterParameters();
        filterParams.Criteria
            .Where(c => !c.Field.Equals(STATUS_FIELD_NAME, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .ForEach(nonStatusFilters.AddFilter);

        Query
            .Filter(nonStatusFilters)
            .AsNoTracking();
    }
}
