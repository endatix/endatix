namespace Endatix.Core.Infrastructure.Paging;

/// <summary>
/// Shared paging and search limits for list endpoints and queries.
/// </summary>
public static class PagedRequestLimits
{
    public const int DEFAULT_PAGE = 1;

    public const int DEFAULT_PAGE_SIZE = 10;

    public const int MIN_PAGE_SIZE = 1;

    public const int MAX_PAGE_SIZE = 100;

    public const int MAX_SEARCH_LENGTH = 256;
}
