using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Filters;

namespace Endatix.Core.Specifications;

public sealed class FormsSpec : Specification<Form>
{
    public FormsSpec(PagingFilter filter)
    {
        Query
         .OrderByDescending(x => x.CreatedAt)
         .Paginate(filter)
         .AsNoTracking();
    }
}
