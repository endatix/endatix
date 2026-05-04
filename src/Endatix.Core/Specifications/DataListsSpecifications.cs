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
    /// Base specification to list data lists without pagination.
    /// </summary>
    public sealed class ListSpec : Specification<DataList>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ListSpec"/> class.
        /// </summary>
        public ListSpec()
        {
            Query
                 .OrderByDescending(x => x.CreatedAt)
                 .AsNoTracking();
        }
    }

    /// <summary>
    /// Specification to get paged data lists projected to DTO without loading all items.
    /// </summary>
    public sealed class ListWithPagingToDtoSpec : Specification<DataList, DataListDto>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ListWithPagingToDtoSpec"/> class.
        /// </summary>
        /// <param name="pagingParams">The paging parameters.</param>
        public ListWithPagingToDtoSpec(PagingParameters pagingParams)
        {
            Query
                .OrderByDescending(x => x.CreatedAt)
                .Paginate(pagingParams)
                .AsNoTracking();

            Query.Select(dataList => new DataListDto(
                dataList.Id,
                dataList.Name,
                dataList.Description,
                dataList.CreatedAt,
                dataList.ModifiedAt,
                dataList.IsActive,
                dataList.Items.Count,
                Array.Empty<DataListItemDto>()));
        }
    }


    /// <summary>
    /// Specification to get a data list by name.
    /// </summary>
    public sealed class ByNameSpec : SingleResultSpecification<DataList>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ByNameSpec"/> class.
        /// </summary>
        /// <param name="name">The name of the data list.</param>
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
        /// <summary>
        /// Initializes a new instance of the <see cref="ExistsSpec"/> class.
        /// </summary>
        /// <param name="dataListId">The ID of the data list.</param>
        public ExistsSpec(long dataListId)
        {
            Query.Where(x => x.Id == dataListId);
            Query.AsNoTracking();
        }
    }

    /// <summary>
    /// Counts data lists that match all given ids and belong to the tenant.
    /// </summary>
    public sealed class ByIdsForTenantSpec : Specification<DataList>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ByIdsForTenantSpec"/> class.
        /// </summary>
        public ByIdsForTenantSpec(IReadOnlyCollection<long> dataListIds, long tenantId)
        {
            Query.Where(x => dataListIds.Contains(x.Id) && x.TenantId == tenantId);
            Query.AsNoTracking();
        }
    }

    /// <summary>
    /// Specification to get a data list by ID with data list items included.
    /// </summary>
    public sealed class ByIdWithItemsSpec : Specification<DataList>, ISingleResultSpecification<DataList>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ByIdWithItemsSpec"/> class.
        /// </summary>
        /// <param name="dataListId">The ID of the data list.</param>
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
        /// <summary>
        /// Initializes a new instance of the <see cref="ByIdWithItemsByValuesSpec"/> class.
        /// </summary>
        /// <param name="dataListId">The ID of the data list.</param>
        /// <param name="values">The values to filter the data list items by.</param>
        public ByIdWithItemsByValuesSpec(long dataListId, IReadOnlyCollection<string> values)
        {
            Query.Where(x => x.Id == dataListId && x.IsActive);

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
        /// <summary>
        /// Initializes a new instance of the <see cref="ToDataListDtoSpec"/> class.
        /// </summary>
        public ToDataListDtoSpec()
        {
            Query.Select(dataList =>
                new DataListDto(
                                dataList.Id,
                                dataList.Name,
                                dataList.Description,
                                dataList.CreatedAt,
                                dataList.ModifiedAt,
                                dataList.IsActive,
                                dataList.Items.Count,
                                dataList.Items.Select(x => new DataListItemDto(x.Id, x.Label, x.Value)).ToArray())
                );
        }
    }
}