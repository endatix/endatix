using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Specifications;

public sealed class FormsSpec : Specification<Form>
{
    public FormsSpec(PagingParameters pagingParams)
    {
        Query
         .OrderByDescending(x => x.CreatedAt)
         .Paginate(pagingParams)
         .AsNoTracking();
    }
}
