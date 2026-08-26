using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Common.Translations;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.DataLists.Search;
using Endatix.Infrastructure.Data.Querying;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data.Repositories;

/// <summary>
/// Repository for data lists with efficient query operations using DbContext.
/// </summary>
public sealed class DataListRepository(
    AppDbContext dbContext,
    IRelationalJsonObjectKeyFilter jsonObjectKeyFilter) : IDataListRepository
{
    /// <inheritdoc />
    public async Task<DataListSearchPageResult?> SearchItemsAsync(
        DataListSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var dataList = await dbContext.DataLists
            .AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.Id == criteria.DataListId && (!criteria.RequireActive || d.IsActive),
                cancellationToken);

        if (dataList is null)
        {
            return null;
        }

        var displayKey = dataList.ResolveLabelSearchKey(criteria.Locale);
        var searchKeys = BuildDistinctKeys(
        [
            SurveyJsTranslationKeys.DefaultKey,
            displayKey,
            .. dataList.ResolveTranslationKeys(criteria.IncludeLocales)
        ]);
        var textKeys = searchKeys;

        var filteredItems = BuildFilteredItemsQuery(criteria, searchKeys);
        filteredItems = ApplyCreatedRange(filteredItems, criteria.CreatedFrom, criteria.CreatedTo);
        filteredItems = ApplyModifiedRange(filteredItems, criteria.ModifiedFrom, criteria.ModifiedTo);

        var total = await filteredItems.CountAsync(cancellationToken);

        var pageItems = await ApplyOrdering(filteredItems, criteria, displayKey)
            .Skip(criteria.Skip)
            .Take(criteria.Take)
            .ToArrayAsync(cancellationToken);

        DataListSearchItemResult[] results = [.. pageItems
            .Select(i => new DataListSearchItemResult(
                i.Id,
                new Dictionary<string, string>(i.Labels, StringComparer.Ordinal),
                i.Value))];

        return new DataListSearchPageResult(criteria.DataListId, total, results, textKeys);
    }

    private IQueryable<DataListItem> BuildFilteredItemsQuery(
        DataListSearchCriteria criteria,
        IReadOnlyList<string> searchKeys)
    {
        var query = dbContext.DataListItems
            .AsNoTracking()
            .Where(i => i.DataListId == criteria.DataListId);

        if (string.IsNullOrWhiteSpace(criteria.Query))
        {
            return query;
        }

        // Match Value or any search key: Labels.default is always included, plus locale / includeLocales keys.
        return jsonObjectKeyFilter.WhereTextOrKeysMatch(
            query,
            nameof(DataListItem.Value),
            nameof(DataListItem.LabelsJson),
            searchKeys,
            criteria.Query.Trim(),
            ToRelationalMode(criteria.MatchMode));
    }

    private IOrderedQueryable<DataListItem> ApplyOrdering(
        IQueryable<DataListItem> query,
        DataListSearchCriteria criteria,
        string displayKey)
    {
        // Default: label (display key) then value.
        if (criteria.SortBy is null)
        {
            return jsonObjectKeyFilter.OrderByKeyThenBy(
                query,
                nameof(DataListItem.LabelsJson),
                displayKey,
                nameof(DataListItem.Value));
        }

        return criteria.SortBy.Value switch
        {
            DataListItemListSortBy.Value when criteria.SortDescending =>
                query.OrderByDescending(item => item.Value).ThenBy(item => item.Id),
            DataListItemListSortBy.Value =>
                query.OrderBy(item => item.Value).ThenBy(item => item.Id),
            DataListItemListSortBy.CreatedAt when criteria.SortDescending =>
                query.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id),
            DataListItemListSortBy.CreatedAt =>
                query.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id),
            DataListItemListSortBy.ModifiedAt when criteria.SortDescending =>
                query.OrderByDescending(item => item.ModifiedAt).ThenBy(item => item.Id),
            DataListItemListSortBy.ModifiedAt =>
                query.OrderBy(item => item.ModifiedAt).ThenBy(item => item.Id),
            DataListItemListSortBy.Label =>
                jsonObjectKeyFilter.OrderByKey(
                        query,
                        nameof(DataListItem.LabelsJson),
                        displayKey,
                        criteria.SortDescending)
                    .ThenBy(item => item.Id),
            _ =>
                jsonObjectKeyFilter.OrderByKey(
                        query,
                        nameof(DataListItem.LabelsJson),
                        displayKey,
                        criteria.SortDescending)
                    .ThenBy(item => item.Id),
        };
    }

    private static IQueryable<DataListItem> ApplyCreatedRange(
        IQueryable<DataListItem> query,
        DateTime? createdFrom,
        DateTime? createdToExclusive)
    {
        if (createdFrom.HasValue)
        {
            var from = createdFrom.Value;
            query = query.Where(item => item.CreatedAt >= from);
        }

        if (createdToExclusive.HasValue)
        {
            var to = createdToExclusive.Value;
            query = to == DateTime.MaxValue
                ? query.Where(item => item.CreatedAt <= to)
                : query.Where(item => item.CreatedAt < to);
        }

        return query;
    }

    private static IQueryable<DataListItem> ApplyModifiedRange(
        IQueryable<DataListItem> query,
        DateTime? modifiedFrom,
        DateTime? modifiedToExclusive)
    {
        if (modifiedFrom.HasValue)
        {
            var from = modifiedFrom.Value;
            query = query.Where(item => item.ModifiedAt != null && item.ModifiedAt >= from);
        }

        if (modifiedToExclusive.HasValue)
        {
            var to = modifiedToExclusive.Value;
            query = to == DateTime.MaxValue
                ? query.Where(item => item.ModifiedAt != null && item.ModifiedAt <= to)
                : query.Where(item => item.ModifiedAt != null && item.ModifiedAt < to);
        }

        return query;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListDistinctLocalesAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.DataLists
            .AsNoTracking()
            .Select(dataList => new
            {
                dataList.DefaultLocale,
                dataList.AvailableLocales
            })
            .ToListAsync(cancellationToken);

        return [.. rows
            .SelectMany(row => row.AvailableLocales.Prepend(row.DefaultLocale))
            .Where(locale => !string.IsNullOrWhiteSpace(locale))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(locale => locale, StringComparer.Ordinal)];
    }

    private static IReadOnlyList<string> BuildDistinctKeys(IEnumerable<string> keys) =>
        [.. keys.Distinct(StringComparer.Ordinal)];

    private static RelationalTextMatchMode ToRelationalMode(DataListSearchMatchMode matchMode) =>
        matchMode switch
        {
            DataListSearchMatchMode.Exact => RelationalTextMatchMode.Exact,
            DataListSearchMatchMode.StartsWith => RelationalTextMatchMode.StartsWith,
            _ => RelationalTextMatchMode.Contains
        };
}
