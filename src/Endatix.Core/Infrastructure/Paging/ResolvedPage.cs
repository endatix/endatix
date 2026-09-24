using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Infrastructure.Paging;

/// <summary>
/// Page window after clamping to the rows that exist. Skip is never negative.
/// </summary>
public readonly record struct ResolvedPage(int Page, int PageSize, int Skip)
{
    public static ResolvedPage For(int page, int pageSize, int totalRecords)
    {
        var resolvedPage = Paged<object>.ResolvePage(page, pageSize, totalRecords);
        return new ResolvedPage(resolvedPage, pageSize, SafeSkip(resolvedPage, pageSize));
    }

    public static int SafeSkip(int page, int pageSize)
    {
        if (page <= 1 || pageSize <= 0)
        {
            return 0;
        }

        var skip = (long)(page - 1) * pageSize;
        return skip > int.MaxValue ? int.MaxValue : (int)skip;
    }

    public Paged<T> ToPaged<T>(int totalRecords, IReadOnlyList<T> items)
    {
        if (items.Count > 0 && (long)Skip + items.Count > totalRecords)
        {
            totalRecords = Skip + items.Count;
        }

        return Paged<T>.FromPage(Page, PageSize, totalRecords, items);
    }
}
