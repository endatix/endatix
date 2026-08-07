using Endatix.Core.Abstractions.Repositories;
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
        long dataListId,
        string? searchQuery,
        int skip,
        int take,
        DataListSearchMatchMode matchMode = DataListSearchMatchMode.Contains,
        string? locale = null,
        CancellationToken cancellationToken = default)
    {
        var dataList = await dbContext.DataLists
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dataListId && d.IsActive, cancellationToken);

        if (dataList is null)
        {
            return null;
        }

        var labelKey = dataList.ResolveLabelSearchKey(locale);
        var filteredItems = BuildFilteredItemsQuery(dataListId, searchQuery, matchMode, labelKey);

        var total = await filteredItems
            .CountAsync(cancellationToken);

        var pageItems = await jsonObjectKeyFilter
            .OrderByKeyThenBy(
                filteredItems,
                nameof(DataListItem.LabelsJson),
                labelKey,
                nameof(DataListItem.Value))
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);

        DataListSearchItemResult[] results = pageItems
            .Select(i => new DataListSearchItemResult(
                i.Id,
                new Dictionary<string, string>(i.Labels, StringComparer.Ordinal),
                i.Value))
            .ToArray();

        return new DataListSearchPageResult(dataListId, total, results);
    }

    private IQueryable<DataListItem> BuildFilteredItemsQuery(
        long dataListId,
        string? searchQuery,
        DataListSearchMatchMode matchMode,
        string labelKey)
    {
        var query = dbContext.DataListItems
            .AsNoTracking()
            .Where(i => i.DataListId == dataListId);

        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return query;
        }

        var trimmed = searchQuery.Trim();
        var textMatchMode = ToRelationalMode(matchMode);

        // Labels-only: match the resolved locale key (default or catalog culture).
        return jsonObjectKeyFilter.WhereKeyMatches(
            query,
            nameof(DataListItem.LabelsJson),
            labelKey,
            trimmed,
            textMatchMode);
    }

    private static RelationalTextMatchMode ToRelationalMode(DataListSearchMatchMode matchMode) =>
        matchMode switch
        {
            DataListSearchMatchMode.Exact => RelationalTextMatchMode.Exact,
            DataListSearchMatchMode.StartsWith => RelationalTextMatchMode.StartsWith,
            _ => RelationalTextMatchMode.Contains
        };
}
