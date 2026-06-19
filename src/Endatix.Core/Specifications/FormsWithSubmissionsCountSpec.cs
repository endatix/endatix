using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.UseCases.Forms;

namespace Endatix.Core.Specifications;

public sealed class FormsWithSubmissionsCountSpec : Specification<Form, FormDto>
{
    public FormsWithSubmissionsCountSpec(
        PagingParameters pagingParams,
        FilterParameters filterParams,
        string? search = null)
    {
        Query.Filter(filterParams);
        FormsListFilterSpec.ApplyNameSearch(Query, search);
        Query.OrderByDescending(form => form.CreatedAt);
        Query.Paginate(pagingParams);
        Query.AsNoTracking();
        Query.Select(FormProjections.ToFormDtoWithSubmissionsCount);
    }
}
