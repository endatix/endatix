using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.UseCases.Forms;

namespace Endatix.Core.Specifications;

public sealed class FormsWithSubmissionsCountSpec : Specification<Form, FormDto>
{
    public FormsWithSubmissionsCountSpec(PagingParameters pagingParams, FilterParameters filterParams)
    {
        Query
            .Filter(filterParams)
            .OrderByDescending(x => x.CreatedAt)
            .Paginate(pagingParams)
            .AsNoTracking();

        Query.Select(FormProjections.ToFormDtoWithSubmissionsCount);
    }
}

