using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Count, clamp the page, then skip and take. Callers pass an already filtered query.
/// </summary>
public static class QueryablePaging
{
    public static async Task<Paged<TResult>> ToPagedAsync<TSource, TResult>(
        this IQueryable<TSource> filtered,
        PageRequest paging,
        Func<IQueryable<TSource>, IQueryable<TSource>> orderBy,
        Func<TSource, TResult> map,
        CancellationToken cancellationToken)
    {
        var totalCount = await filtered.CountAsync(cancellationToken);
        var window = paging.ForTotal(totalCount);

        IReadOnlyList<TSource> rows = [];
        if (totalCount > 0)
        {
            rows = await orderBy(filtered)
                .Skip(window.Skip)
                .Take(window.PageSize)
                .ToListAsync(cancellationToken);
        }

        return window.ToPaged(totalCount, rows.Select(map).ToList());
    }
}
