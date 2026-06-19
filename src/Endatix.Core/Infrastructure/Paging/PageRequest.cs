namespace Endatix.Core.Infrastructure.Paging;

/// <summary>
/// Normalized page-based paging input for list queries and read models.
/// </summary>
public sealed record PageRequest
{
    /// <summary>
    /// The page number.
    /// </summary>
    public int Page { get; }

    /// <summary>
    /// The page size.
    /// </summary>
    public int PageSize { get; }

    /// <summary>
    /// The number of items to skip.
    /// </summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    /// Creates a new <see cref="PageRequest"/> from a page and page size.
    /// </summary>
    /// <param name="page">The page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns>A new <see cref="PageRequest"/>.</returns>
    public PageRequest(int? page, int? pageSize)
    {
        Page = Math.Max(page ?? PagedRequestLimits.DEFAULT_PAGE, PagedRequestLimits.DEFAULT_PAGE);
        PageSize = Math.Clamp(
            pageSize ?? PagedRequestLimits.DEFAULT_PAGE_SIZE,
            PagedRequestLimits.MIN_PAGE_SIZE,
            PagedRequestLimits.MAX_PAGE_SIZE);
    }

    /// <summary>
    /// Creates a new <see cref="PageRequest"/> from a skip and take.
    /// </summary>
    /// <param name="skip">The number of items to skip.</param>
    /// <param name="take">The number of items to take.</param>
    /// <returns>A new <see cref="PageRequest"/>.</returns>
    public static PageRequest FromSkipTake(int skip, int take)
    {
        var normalizedTake = Math.Clamp(
            take,
            PagedRequestLimits.MIN_PAGE_SIZE,
            PagedRequestLimits.MAX_PAGE_SIZE);
        var normalizedSkip = Math.Max(skip, 0);
        var page = (normalizedSkip / normalizedTake) + 1;

        return new PageRequest(page, normalizedTake);
    }
}
