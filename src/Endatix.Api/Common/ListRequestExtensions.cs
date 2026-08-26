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
    public static int ResolvePage(this IPagedRequest request) =>
        Math.Max(request.Page ?? PagedRequestLimits.DEFAULT_PAGE, PagedRequestLimits.DEFAULT_PAGE);

    /// <summary>
    /// Resolves the page size from the request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The resolved page size.</returns>
    public static int ResolvePageSize(this IPagedRequest request) =>
        Math.Clamp(
            value: request.PageSize ?? PagedRequestLimits.DEFAULT_PAGE_SIZE,
            min: PagedRequestLimits.MIN_PAGE_SIZE,
            max: PagedRequestLimits.MAX_PAGE_SIZE);

    /// <summary>
    /// Converts the request to a <see cref="PageRequest"/>.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The converted <see cref="PageRequest"/>.</returns>
    public static PageRequest ToPageRequest(this IPagedRequest request) =>
        new(request.ResolvePage(), request.ResolvePageSize());

    /// <summary>
    /// Converts the request to a <see cref="SearchablePageRequest"/>.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The converted <see cref="SearchablePageRequest"/>.</returns>
    public static SearchablePageRequest ToSearchablePageRequest<TRequest>(this TRequest request)
        where TRequest : IPagedRequest, ISearchableRequest =>
        new(request.ResolvePage(), request.ResolvePageSize(), request.Search);

    /// <summary>
    /// Converts the request to a <see cref="SortRequest{TSortField}"/>.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="defaultField">The default field.</param>
    /// <param name="defaultDirection">The default direction.</param>
    /// <returns>The converted <see cref="SortRequest{TSortField}"/>.</returns>
    public static SortRequest<TSortField> ToSortRequest<TSortField>(
        this ISortableRequest<TSortField> request,
        TSortField defaultField,
        SortDirection defaultDirection = SortDirection.Asc)
        where TSortField : struct, Enum =>
        SortRequest<TSortField>.FromNullableOrDefault(
            request.SortBy,
            request.SortDir,
            defaultField,
            defaultDirection);

    /// <summary>
    /// Normalized sort when <c>sortBy</c> or <c>sortDir</c> is supplied, and <see langword="null"/> only when
    /// both are omitted. Use for lists that keep a bespoke default ordering (a multi-key order that no single
    /// sort field reproduces), so that <c>sortDir</c> alone still flips the default field instead of being dropped.
    /// </summary>
    public static SortRequest<TSortField>? ToNullableSortRequest<TSortField>(
        this ISortableRequest<TSortField> request,
        TSortField defaultField,
        SortDirection defaultDirection = SortDirection.Asc)
        where TSortField : struct, Enum =>
        SortRequest<TSortField>.FromNullable(
            request.SortBy,
            request.SortDir,
            defaultField,
            defaultDirection);

    public static FilterParameters ToFilterParameters(this IFilterable request) =>
        new(request.Filter ?? []);

    /// <summary>
    /// Inclusive UTC start of <see cref="ICreatedRange.CreatedFrom"/>.
    /// </summary>
    public static DateTime? ToCreatedFromUtc(this ICreatedRange request) =>
        UtcCalendarDay.InclusiveStartUtc(request.CreatedFrom);

    /// <summary>
    /// Exclusive UTC end of <see cref="ICreatedRange.CreatedTo"/>.
    /// </summary>
    public static DateTime? ToCreatedToUtc(this ICreatedRange request) =>
        UtcCalendarDay.ExclusiveEndUtc(request.CreatedTo);

    /// <summary>
    /// Inclusive UTC start of <see cref="IModifiedRange.ModifiedFrom"/>.
    /// </summary>
    public static DateTime? ToModifiedFromUtc(this IModifiedRange request) =>
        UtcCalendarDay.InclusiveStartUtc(request.ModifiedFrom);

    /// <summary>
    /// Exclusive UTC end of <see cref="IModifiedRange.ModifiedTo"/>.
    /// </summary>
    public static DateTime? ToModifiedToUtc(this IModifiedRange request) =>
        UtcCalendarDay.ExclusiveEndUtc(request.ModifiedTo);

    /// <summary>
    /// Inclusive UTC start of <see cref="IStartedRange.StartedFrom"/>.
    /// </summary>
    public static DateTime? ToStartedFromUtc(this IStartedRange request) =>
        UtcCalendarDay.InclusiveStartUtc(request.StartedFrom);

    /// <summary>
    /// Exclusive UTC end of <see cref="IStartedRange.StartedTo"/>.
    /// </summary>
    public static DateTime? ToStartedToUtc(this IStartedRange request) =>
        UtcCalendarDay.ExclusiveEndUtc(request.StartedTo);

    /// <summary>
    /// Inclusive UTC start of <see cref="ICompletedRange.CompletedFrom"/>.
    /// </summary>
    public static DateTime? ToCompletedFromUtc(this ICompletedRange request) =>
        UtcCalendarDay.InclusiveStartUtc(request.CompletedFrom);

    /// <summary>
    /// Exclusive UTC end of <see cref="ICompletedRange.CompletedTo"/>.
    /// </summary>
    public static DateTime? ToCompletedToUtc(this ICompletedRange request) =>
        UtcCalendarDay.ExclusiveEndUtc(request.CompletedTo);

    /// <summary>
    /// Inclusive UTC start of <see cref="ILastLoginRange.LastLoginFrom"/>.
    /// </summary>
    public static DateTime? ToLastLoginFromUtc(this ILastLoginRange request) =>
        UtcCalendarDay.InclusiveStartUtc(request.LastLoginFrom);

    /// <summary>
    /// Exclusive UTC end of <see cref="ILastLoginRange.LastLoginTo"/>.
    /// </summary>
    public static DateTime? ToLastLoginToUtc(this ILastLoginRange request) =>
        UtcCalendarDay.ExclusiveEndUtc(request.LastLoginTo);
}
