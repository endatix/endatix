using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Specifications;

public class SubmissionsByFormIdCountSpec : Specification<Submission>, ISubmitterProfileFilterSpecification
{
    private const string SUBMITTER_PROFILE_FIELD_PREFIX = "submitterProfile.";

    public IReadOnlyList<FilterCriterion> SubmitterProfileFilters { get; }

    public SubmissionsByFormIdCountSpec(long formId, FilterParameters filterParams)
    {
        SubmitterProfileFilters = filterParams.Criteria
            .Where(c => c.Field.StartsWith(SUBMITTER_PROFILE_FIELD_PREFIX, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Query
            .WhereFormIdAndFilters(formId, filterParams)
            .AsNoTracking();
    }
}
