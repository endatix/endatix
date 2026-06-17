using Endatix.Core.Infrastructure.Paging;

namespace Endatix.Api.Common;

/// <summary>
/// Resolves validated paging values for endpoint execution.
/// </summary>
public static class PagedRequestExtensions
{
    public static int ResolvePage(this IPagedRequest request) =>
        Math.Max(request.Page ?? PagedRequestLimits.DEFAULT_PAGE, PagedRequestLimits.DEFAULT_PAGE);

    public static int ResolvePageSize(this IPagedRequest request) =>
        Math.Clamp(
            value: request.PageSize ?? PagedRequestLimits.DEFAULT_PAGE_SIZE,
            min: PagedRequestLimits.MIN_PAGE_SIZE,
            max: PagedRequestLimits.MAX_PAGE_SIZE);
}
