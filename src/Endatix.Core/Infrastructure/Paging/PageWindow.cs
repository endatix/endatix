using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Infrastructure.Paging;

/// <summary>
/// The rows a list returns after counting: the requested page clamped to the last page that exists.
/// Build it with <see cref="PageRequest.ForTotal"/> once the count is known, fetch with
/// <see cref="Skip"/> / <see cref="PageSize"/>, then call <see cref="ToPaged{T}"/>.
/// </summary>
public readonly record struct PageWindow
{
    private PageWindow(int page, int pageSize, int totalRecords)
    {
        Page = page;
        PageSize = pageSize;
        TotalRecords = totalRecords;
    }

    /// <summary>Page to fetch and report; between 1 and the last page.</summary>
    public int Page { get; }

    public int PageSize { get; }

    /// <summary>Rows that matched the count query.</summary>
    public int TotalRecords { get; }

    /// <summary>Offset for the fetch. Never negative and never past the last row.</summary>
    public int Skip => SkipFor(Page, PageSize);

    /// <summary>
    /// Clamps <paramref name="page"/> to the last page that <paramref name="totalRecords"/> fills.
    /// A page below 1 reads the first page.
    /// </summary>
    public static PageWindow For(int page, int pageSize, int totalRecords)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        ArgumentOutOfRangeException.ThrowIfNegative(totalRecords);

        var lastPage = totalRecords == 0 ? 1 : (int)((totalRecords + (long)pageSize - 1) / pageSize);
        return new PageWindow(Math.Clamp(page, 1, lastPage), pageSize, totalRecords);
    }

    /// <summary>
    /// Builds the response page. A row committed between the count and the fetch raises the
    /// total instead of failing the request; a row deleted in that gap leaves the page short.
    /// </summary>
    public Paged<T> ToPaged<T>(IReadOnlyList<T> items)
    {
        var totalRecords = Math.Max(TotalRecords, (int)Math.Min(int.MaxValue, (long)Skip + items.Count));
        return Paged<T>.FromPage(Page, PageSize, totalRecords, items);
    }

    internal static int SkipFor(int page, int pageSize) =>
        page <= 1 ? 0 : (int)Math.Min(int.MaxValue, (long)(page - 1) * pageSize);
}
