using Endatix.Core.Infrastructure.Paging;

namespace Endatix.Core.Specifications.Parameters;

/// <summary>
/// Base Paging Filter class to be used when applying Specification. It contains Paging and PageSize
/// </summary>
public class PagingParameters
{
    public PagingParameters(int? page, int? pageSize) : this(page ?? FIRST_PAGE, pageSize ?? DEFAULT_PAGE_SIZE)
    {
    }

    public PagingParameters(int page, int pageSize)
    {
        Page = (page <= default(int)) ? FIRST_PAGE : page;
        PageSize = (pageSize <= default(int)) ? DEFAULT_PAGE_SIZE : pageSize;
    }

    /// <summary>
    /// Fetches the clamped page of a counted list.
    /// </summary>
    public PagingParameters(PageWindow window) : this(window.Page, window.PageSize)
    {
    }

    public int Page { get; init; } = FIRST_PAGE;

    public int PageSize { get; init; } = DEFAULT_PAGE_SIZE;

    public const int DEFAULT_PAGE_SIZE = 10;

    /// <summary>
    /// The window to fetch once the list is counted. See <see cref="PageWindow"/>.
    /// </summary>
    public PageWindow ForTotal(int totalRecords) => PageWindow.For(Page, PageSize, totalRecords);

    public const int FIRST_PAGE = 1;
}
