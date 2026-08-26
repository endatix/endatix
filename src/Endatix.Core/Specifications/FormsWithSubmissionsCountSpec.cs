using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.UseCases.Forms;
using Endatix.Core.UseCases.Forms.List;

namespace Endatix.Core.Specifications;

public sealed class FormsWithSubmissionsCountSpec : Specification<Form, FormDto>
{
    public FormsWithSubmissionsCountSpec(
        PagingParameters pagingParams,
        FilterParameters filterParams,
        string? search = null,
        FormListSortBy sortBy = FormListSortBy.CreatedAt,
        bool sortDescending = true,
        UtcDateTimeRange created = default,
        UtcDateTimeRange modified = default)
    {
        Query.Filter(filterParams);
        FormsListFilterSpec.ApplyNameSearch(Query, search);
        Query.WhereUtcRange(x => x.CreatedAt, created);
        Query.WhereUtcRange(x => x.ModifiedAt, modified);
        FormsListFilterSpec.ApplyOrdering(Query, sortBy, sortDescending);
        Query.Paginate(pagingParams);
        Query.AsNoTracking();
        Query.Select(FormProjections.ToFormDtoWithSubmissionsCount);
    }
}
