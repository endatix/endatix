using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data.Querying;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data.Repositories;

/// <summary>
/// Repository for data lists with efficient query operations using DbContext.
/// </summary>
public sealed class DataListRepository(
    AppDbContext dbContext,
    IRelationalSubstringLikeFilter substringLikeFilter) : IDataListRepository
{
    /// <inheritdoc />
    public async Task<DataListSearchPageResult?> SearchItemsAsync(
        long dataListId,
        string? searchQuery,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var dataListExists = await dbContext.DataLists
            .AsNoTracking()
            .AnyAsync(d => d.Id == dataListId && d.IsActive, cancellationToken);

        if (!dataListExists)
        {
            return null;
        }

        var filteredItems = BuildFilteredItemsQuery(dataListId, searchQuery);

        var total = await filteredItems
            .CountAsync(cancellationToken);

        var pageItems = await filteredItems
            .OrderBy(i => i.Label)
            .ThenBy(i => i.Value)
            .Skip(skip)
            .Take(take)
            .Select(i => new DataListSearchItemResult(i.Id, i.Label, i.Value))
            .ToArrayAsync(cancellationToken);

        return new DataListSearchPageResult(dataListId, total, pageItems);
    }

    private IQueryable<DataListItem> BuildFilteredItemsQuery(long dataListId, string? searchQuery)
    {
        var query = dbContext.DataListItems
            .AsNoTracking()
            .Where(i => i.DataListId == dataListId);

        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return query;
        }

        return substringLikeFilter.WherePropertyMatchesLikeSubstring(
            query,
            nameof(DataListItem.Label),
            searchQuery.Trim());
    }
}
