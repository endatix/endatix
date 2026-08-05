using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Common.Translations;
using Endatix.Core.Entities;
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
        CancellationToken cancellationToken = default)
    {
        bool dataListExists = await dbContext.DataLists
            .AsNoTracking()
            .AnyAsync(d => d.Id == dataListId && d.IsActive, cancellationToken);

        if (!dataListExists)
        {
            return null;
        }

        IQueryable<DataListItem> filteredItems = BuildFilteredItemsQuery(dataListId, searchQuery);

        int total = await filteredItems
            .CountAsync(cancellationToken);

        DataListItem[] pageItems = await jsonObjectKeyFilter
            .OrderByKeyThenBy(
                filteredItems,
                nameof(DataListItem.LabelsJson),
                SurveyJsTranslationKeys.DefaultKey,
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

    private IQueryable<DataListItem> BuildFilteredItemsQuery(long dataListId, string? searchQuery)
    {
        IQueryable<DataListItem> query = dbContext.DataListItems
            .AsNoTracking()
            .Where(i => i.DataListId == dataListId);

        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return query;
        }

        string trimmed = searchQuery.Trim();

        // Match Value or Labels['default'] only — other locales are ignored until PR-2 includeLocales.
        return jsonObjectKeyFilter.WhereKeyOrPropertyMatches(
            query,
            nameof(DataListItem.Value),
            nameof(DataListItem.LabelsJson),
            SurveyJsTranslationKeys.DefaultKey,
            trimmed);
    }
}
