using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Api.Common;

/// <summary>
/// Maps composable list request capabilities to normalized Core types.
/// </summary>
public static class ListRequestExtensions
{
    /// <summary>
    /// Resolves the page number from the request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The resolved page number.</returns>
    public static int ResolvePage(this IPageable request) =>
        Math.Max(request.Page ?? PagedRequestLimits.DEFAULT_PAGE, PagedRequestLimits.DEFAULT_PAGE);

    /// <summary>
    /// Resolves the page size from the request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The resolved page size.</returns>
    public static int ResolvePageSize(this IPageable request) =>
        Math.Clamp(
            value: request.PageSize ?? PagedRequestLimits.DEFAULT_PAGE_SIZE,
            min: PagedRequestLimits.MIN_PAGE_SIZE,
            max: PagedRequestLimits.MAX_PAGE_SIZE);

    /// <summary>
    /// Converts the request to a <see cref="PageRequest"/>.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The converted <see cref="PageRequest"/>.</returns>
    public static PageRequest ToPageRequest(this IPageable request) =>
        new(request.ResolvePage(), request.ResolvePageSize());

    /// <summary>
    /// Converts the request to a <see cref="SearchablePageRequest"/>.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The converted <see cref="SearchablePageRequest"/>.</returns>
    public static SearchablePageRequest ToSearchablePageRequest<TRequest>(this TRequest request)
        where TRequest : IPageable, ISearchable =>
        new(request.ResolvePage(), request.ResolvePageSize(), request.Search);

    /// <summary>
    /// Converts the request to a <see cref="SortRequest{TSortField}"/>.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="defaultField">The default field.</param>
    /// <param name="defaultDirection">The default direction.</param>
    /// <returns>The converted <see cref="SortRequest{TSortField}"/>.</returns>
    public static SortRequest<TSortField> ToSortRequest<TSortField>(
        this ISortable<TSortField> request,
        TSortField defaultField,
        SortDirection defaultDirection = SortDirection.Asc)
        where TSortField : struct, Enum =>
        SortRequest<TSortField>.FromNullableOrDefault(
            request.SortBy,
            request.Direction,
            defaultField,
            defaultDirection);

    public static FilterParameters ToFilterParameters(this IFilterable request) =>
        new(request.Filter ?? []);
}
