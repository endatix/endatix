using Endatix.Core.Abstractions.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data.Repositories;

/// <summary>
/// Repository for data lists with efficient query operations using DbContext.
/// </summary>
public sealed class DataListRepository(AppDbContext dbContext) : IDataListRepository
{
    private readonly AppDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<DataListSearchPageResult?> SearchItemsAsync(
        long dataListId,
        string? searchQuery,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var dataListExists = await _dbContext.DataLists
            .AsNoTracking()
            .AnyAsync(d => d.Id == dataListId && d.IsActive, cancellationToken)
            .ConfigureAwait(false);

        if (!dataListExists)
        {
            return null;
        }

        var filteredItems = BuildFilteredItemsQuery(dataListId, searchQuery);

        var total = await filteredItems
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var pageItems = await filteredItems
            .OrderBy(i => i.Label)
            .ThenBy(i => i.Value)
            .Skip(skip)
            .Take(take)
            .Select(i => new DataListSearchItemResult(i.Id, i.Label, i.Value))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return new DataListSearchPageResult(dataListId, total, pageItems);
    }

    private IQueryable<Core.Entities.DataListItem> BuildFilteredItemsQuery(long dataListId, string? searchQuery)
    {
        var query = _dbContext.DataListItems
            .AsNoTracking()
            .Where(i => i.DataListId == dataListId);

        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return query;
        }

        var normalizedQuery = searchQuery.Trim().ToLowerInvariant();
        return query.Where(i =>
            i.Label.ToLower().Contains(normalizedQuery));
    }
}