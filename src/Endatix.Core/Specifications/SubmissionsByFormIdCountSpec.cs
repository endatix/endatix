using Ardalis.Specification;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Specifications;

public class SubmissionsByFormIdCountSpec : SubmissionsByFormIdFilteredSpecBase
{
    public SubmissionsByFormIdCountSpec(long formId, FilterParameters filterParams)
        : base(filterParams)
    {
        Query
            .WhereFormIdAndFilters(formId, filterParams)
            .AsNoTracking();
    }
}
