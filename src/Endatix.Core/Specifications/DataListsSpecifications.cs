using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.UseCases.DataLists;

namespace Endatix.Core.Specifications;


/// <summary>
/// Specifications for working with DataList entities
/// </summary>
public static class DataListsSpecifications
{
    /// <summary>
    /// Base specification to list data lists without pagination.
    /// </summary>
    public sealed class ListSpec : Specification<DataList>
    {
        public ListSpec(string? hasLocale = null, string? search = null)
        {
            ApplyListFilters(Query, hasLocale, search);

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
        public ListWithPagingToDtoSpec(PagingParameters pagingParams, string? hasLocale = null, string? search = null)
        {
            ApplyListFilters(Query, hasLocale, search);

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
                dataList.DefaultLocale,
                dataList.AvailableLocales,
                Array.Empty<DataListItemDto>()));
        }
    }

    private static void ApplyListFilters(
        ISpecificationBuilder<DataList> query,
        string? hasLocale,
        string? search)
    {
        if (!string.IsNullOrWhiteSpace(hasLocale))
        {
            var locale = hasLocale.Trim().ToLowerInvariant();
            query.Where(x => x.AvailableLocales.Contains(locale));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query.Where(x =>
                x.Name.ToLower().Contains(term) ||
                (x.Description != null && x.Description.ToLower().Contains(term)));
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
    /// Specification to get a data list by normalized name.
    /// </summary>
    public sealed class ByNormalizedNameSpec : SingleResultSpecification<DataList>
    {
        public ByNormalizedNameSpec(string normalizedName)
        {
            Query.Where(x => x.NormalizedName == normalizedName);
            Query.AsNoTracking();
        }
    }

    /// <summary>
    /// Specification to check if a data list exists by ID.
    /// </summary>
    public sealed class ExistsSpec : SingleResultSpecification<DataList>
    {
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
        public ByIdsForTenantSpec(IReadOnlyCollection<long> dataListIds, long tenantId)
        {
            Query.Where(x => dataListIds.Contains(x.Id) && x.TenantId == tenantId);
            Query.AsNoTracking();
        }
    }

    /// <summary>
    /// Specification to get a data list by ID with data list items included.
    /// </summary>
    public sealed class ByIdWithItemsSpec : SingleResultSpecification<DataList>
    {
        public ByIdWithItemsSpec(long dataListId)
        {
            Query
                .Where(x => x.Id == dataListId)
                .Include(x => x.Items);
        }
    }

    /// <summary>
    /// Projects a data list by ID without loading item rows. <c>ItemsCount</c> is still computed in SQL.
    /// </summary>
    public sealed class ByIdWithoutItemsToDtoSpec : SingleResultSpecification<DataList, DataListDto>
    {
        public ByIdWithoutItemsToDtoSpec(long dataListId)
        {
            Query
                .Where(x => x.Id == dataListId)
                .AsNoTracking();

            Query.Select(dataList => new DataListDto(
                dataList.Id,
                dataList.Name,
                dataList.Description,
                dataList.CreatedAt,
                dataList.ModifiedAt,
                dataList.IsActive,
                dataList.Items.Count,
                dataList.DefaultLocale,
                dataList.AvailableLocales,
                Array.Empty<DataListItemDto>()));
        }
    }

    /// <summary>
    /// Specification to get a data list by ID with data list items included by values.
    /// </summary>
    public sealed class ByIdWithItemsByValuesSpec : SingleResultSpecification<DataList>
    {
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
                                dataList.DefaultLocale,
                                dataList.AvailableLocales,
                                dataList.Items.Select(x => new DataListItemDto(
                                    x.Id,
                                    x.Labels,
                                    x.Value,
                                    x.DefaultLabel)).ToArray())
                );
        }
    }
}
