using Ardalis.Specification;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.UseCases.Submissions.ListByFormId;

namespace Endatix.Core.Specifications;

public class SubmissionsByFormIdCountSpec : SubmissionsByFormIdFilteredSpecBase
{
    public SubmissionsByFormIdCountSpec(
        long formId,
        FilterParameters filterParams,
        SubmissionsListFilter? listFilter = null)
        : base(filterParams)
    {
        listFilter ??= new SubmissionsListFilter();

        Query
            .WhereFormIdAndFilters(formId, filterParams)
            .ApplyListDateRanges(listFilter)
            .AsNoTracking();
    }
}
