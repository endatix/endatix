using Ardalis.Specification;
using Endatix.Core.Common.Translations;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.List;

namespace Endatix.Core.Specifications;


/// <summary>
/// Specifications for working with DataList entities
/// </summary>
public static class DataListsSpecifications
{
    /// <summary>
    /// Shared list filters / ordering for management grids.
    /// </summary>
    public sealed record ListFilter(
        string? HasLocale = null,
        string? Query = null,
        DataListListSortBy SortBy = DataListListSortBy.CreatedAt,
        bool SortDescending = true,
        DateTime? CreatedFrom = null,
        DateTime? CreatedTo = null,
        DateTime? ModifiedFrom = null,
        DateTime? ModifiedTo = null);

    /// <summary>
    /// Base specification to list data lists without pagination.
    /// </summary>
    public sealed class ListSpec : Specification<DataList>
    {
        public ListSpec(string? hasLocale = null, string? query = null)
            : this(new ListFilter(hasLocale, query))
        {
        }

        public ListSpec(ListFilter filter)
        {
            ApplyListFilters(Query, filter);
            ApplyListOrdering(Query, filter);
            Query.AsNoTracking();
        }
    }

    /// <summary>
    /// Specification to get paged data lists projected to DTO without loading all items.
    /// </summary>
    public sealed class ListWithPagingToDtoSpec : Specification<DataList, DataListDto>
    {
        public ListWithPagingToDtoSpec(
            PagingParameters pagingParams,
            string? hasLocale = null,
            string? query = null)
            : this(pagingParams, new ListFilter(hasLocale, query))
        {
        }

        public ListWithPagingToDtoSpec(PagingParameters pagingParams, ListFilter filter)
        {
            ApplyListFilters(Query, filter);
            ApplyListOrdering(Query, filter);
            Query
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
        ListFilter filter)
    {
        List<string> locales = TranslationLocaleList.ParseMany([filter.HasLocale])
            .Where(code => !code.IsSyntheticDefault)
            .Select(code => code.Value)
            .ToList();

        if (locales.Count > 0)
        {
            query.Where(x => locales.Any(locale =>
                x.AvailableLocales.Contains(locale) || x.DefaultLocale == locale));
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var term = filter.Query.Trim().ToLowerInvariant();
            query.Where(x =>
                x.Name.ToLower().Contains(term) ||
                (x.Description != null && x.Description.ToLower().Contains(term)));
        }

        if (filter.CreatedFrom.HasValue)
        {
            var from = filter.CreatedFrom.Value;
            query.Where(x => x.CreatedAt >= from);
        }

        if (filter.CreatedTo.HasValue)
        {
            var toExclusive = filter.CreatedTo.Value;
            query.Where(x => x.CreatedAt < toExclusive);
        }

        if (filter.ModifiedFrom.HasValue)
        {
            var from = filter.ModifiedFrom.Value;
            query.Where(x => x.ModifiedAt != null && x.ModifiedAt >= from);
        }

        if (filter.ModifiedTo.HasValue)
        {
            var toExclusive = filter.ModifiedTo.Value;
            query.Where(x => x.ModifiedAt != null && x.ModifiedAt < toExclusive);
        }
    }

    private static void ApplyListOrdering(
        ISpecificationBuilder<DataList> query,
        ListFilter filter)
    {
        switch (filter.SortBy)
        {
            case DataListListSortBy.Name:
                if (filter.SortDescending) { query.OrderByDescending(x => x.Name); }
                else { query.OrderBy(x => x.Name); }
                break;
            case DataListListSortBy.ModifiedAt:
                if (filter.SortDescending) { query.OrderByDescending(x => x.ModifiedAt); }
                else { query.OrderBy(x => x.ModifiedAt); }
                break;
            case DataListListSortBy.ItemsCount:
                if (filter.SortDescending) { query.OrderByDescending(x => x.Items.Count); }
                else { query.OrderBy(x => x.Items.Count); }
                break;
            case DataListListSortBy.IsActive:
                if (filter.SortDescending) { query.OrderByDescending(x => x.IsActive); }
                else { query.OrderBy(x => x.IsActive); }
                break;
            case DataListListSortBy.CreatedAt:
            default:
                if (filter.SortDescending) { query.OrderByDescending(x => x.CreatedAt); }
                else { query.OrderBy(x => x.CreatedAt); }
                break;
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
