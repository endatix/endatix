using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Specifications;

/// <summary>
/// Specification to get data lists with paging.
/// </summary>
public sealed class DataListsSpec : Specification<DataList>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataListsSpec"/> class.
    /// </summary>
    /// <param name="pagingParams">The paging parameters.</param>
    public DataListsSpec(PagingParameters pagingParams)
    {
        Query
            .OrderByDescending(x => x.CreatedAt)
            .Paginate(pagingParams)
            .AsNoTracking();
    }
}
