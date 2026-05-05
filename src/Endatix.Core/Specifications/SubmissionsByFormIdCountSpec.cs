using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Specifications;

public class SubmissionsByFormIdCountSpec : Specification<Submission>
{
    public SubmissionsByFormIdCountSpec(long formId, FilterParameters filterParams)
    {
        Query
            .WhereFormIdAndFilters(formId, filterParams)
            .AsNoTracking();
    }
}
