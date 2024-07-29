namespace Endatix.Core.Filters;

/// <summary>
/// Base Paging Filter class to be used when applying Specification. It contains Paging and PageSize
/// </summary>
public class PagingFilter
{
    public PagingFilter(int? page, int? pageSize) : this(page ?? FIRST_PAGE, pageSize ?? DEFAULT_PAGE_SIZE)
    {
    }

    public PagingFilter(int page, int pageSize)
    {
        Page = (page <= default(int)) ? FIRST_PAGE : page;
        PageSize = (pageSize <= default(int)) ? DEFAULT_PAGE_SIZE : pageSize;
    }

    public int Page { get; init; } = FIRST_PAGE;

    public int PageSize { get; init; } = DEFAULT_PAGE_SIZE;

    public const int DEFAULT_PAGE_SIZE = 10;

    public const int FIRST_PAGE = 1;
}
