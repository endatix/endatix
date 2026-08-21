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

        var total = await filteredItems.CountAsync(cancellationToken);

        var pageItems = await jsonObjectKeyFilter
            .OrderByKeyThenBy(
                filteredItems,
                nameof(DataListItem.LabelsJson),
                displayKey,
                nameof(DataListItem.Value))
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
