using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.UseCases.DataLists;

namespace Endatix.Core.Specifications;


/// <summary>
/// Specifications for working with DataList entities
/// </summary>
public class DataListsSpecifications
{
    /// <summary>
    /// Specification to get data lists with paging.
    /// </summary>
    public sealed class WithPagingSpec : Specification<DataList>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WithPagingSpec"/> class.
        /// </summary>
        /// <param name="pagingParams">The paging parameters.</param>
        public WithPagingSpec(PagingParameters pagingParams)
        {
            Query
                .OrderByDescending(x => x.CreatedAt)
                .Paginate(pagingParams)
                .AsNoTracking();
        }
    }


    /// <summary>
    /// Specification to get a data list by name.
    /// </summary>
    public sealed class ByNameSpec : SingleResultSpecification<DataList>
    {
        public ByNameSpec(string name)
        {
            Query.Where(x => x.Name == name);
            Query.AsNoTracking();
        }
    }

    /// <summary>
    /// Specification to check if a data list exists by ID.
    /// </summary>
    public sealed class ExistsSpec : Specification<DataList>, ISingleResultSpecification<DataList>
    {
        public ExistsSpec(long dataListId)
        {
            Query.Where(x => x.Id == dataListId);
            Query.AsNoTracking();
        }
    }

    /// <summary>
    /// Specification to get a data list by ID with data list items included.
    /// </summary>
    public sealed class ByIdWithItemsSpec : Specification<DataList>, ISingleResultSpecification<DataList>
    {
        public ByIdWithItemsSpec(long dataListId)
        {
            Query
                .Where(x => x.Id == dataListId)
                .Include(x => x.Items);
        }
    }


    /// <summary>
    /// Specification to get a data list by ID with data list items included by values.
    /// </summary>
    public sealed class ByIdWithItemsByValuesSpec : Specification<DataList>, ISingleResultSpecification<DataList>
    {
        public ByIdWithItemsByValuesSpec(long dataListId, IReadOnlyCollection<string> values)
        {
            Query.Where(x => x.Id == dataListId);

            if (values.Count == 0)
            {
                Query.Include(x => x.Items.Where(_ => false));
                return;
            }

            Query.Include(x => x.Items.Where(item => values.Contains(item.Value)));
        }
    }

    /// <summary>
    /// Specification to map a data list to a data list DTO.
    /// </summary>
    public sealed class ToDataListDtoSpec : SingleResultSpecification<DataList, DataListDto>
    {
        public ToDataListDtoSpec()
        {
            Query.Select(dataList =>
                new DataListDto(
                                dataList.Id,
                                dataList.Name,
                                dataList.Description,
                                dataList.IsActive,
                                dataList.Items.Select(x => new DataListItemDto(x.Id, x.Label, x.Value)).ToArray())
                );
        }
    }
}