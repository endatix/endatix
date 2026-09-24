using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Pages an EF list the one reliable way: count, clamp the page (<see cref="PageWindow"/>), then order, skip and take.
/// </summary>
public static class QueryablePaging
{
    /// <param name="filtered">The filtered, unordered query. It is counted as-is.</param>
    /// <param name="paging">The requested page.</param>
    /// <param name="orderBy">Total order for the fetch; end with a unique key so pages cannot skip or repeat rows.</param>
    /// <param name="map">Maps each fetched row in memory.</param>
    public static async Task<Paged<TResult>> ToPagedAsync<TSource, TResult>(
        this IQueryable<TSource> filtered,
        PageRequest paging,
        Func<IQueryable<TSource>, IOrderedQueryable<TSource>> orderBy,
        Func<TSource, TResult> map,
        CancellationToken cancellationToken)
    {
        var window = paging.ForTotal(await filtered.CountAsync(cancellationToken));
        if (window.TotalRecords == 0)
        {
            return window.ToPaged<TResult>([]);
        }

        var rows = await orderBy(filtered)
            .Skip(window.Skip)
            .Take(window.PageSize)
            .ToListAsync(cancellationToken);

        return window.ToPaged(rows.ConvertAll(row => map(row)));
    }
}
