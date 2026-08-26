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
    /// Parses <see cref="ICreatedRange"/> calendar days into a <see cref="UtcDateTimeRange"/>.
    /// </summary>
    public static UtcDateTimeRange ToCreatedRange(this ICreatedRange request) =>
        ToUtcDateTimeRange(request.CreatedFrom, request.CreatedTo);

    /// <summary>
    /// Parses <see cref="IModifiedRange"/> calendar days into a <see cref="UtcDateTimeRange"/>.
    /// </summary>
    public static UtcDateTimeRange ToModifiedRange(this IModifiedRange request) =>
        ToUtcDateTimeRange(request.ModifiedFrom, request.ModifiedTo);

    /// <summary>
    /// Parses <see cref="IStartedRange"/> calendar days into a <see cref="UtcDateTimeRange"/>.
    /// </summary>
    public static UtcDateTimeRange ToStartedRange(this IStartedRange request) =>
        ToUtcDateTimeRange(request.StartedFrom, request.StartedTo);

    /// <summary>
    /// Parses <see cref="ICompletedRange"/> calendar days into a <see cref="UtcDateTimeRange"/>.
    /// </summary>
    public static UtcDateTimeRange ToCompletedRange(this ICompletedRange request) =>
        ToUtcDateTimeRange(request.CompletedFrom, request.CompletedTo);

    /// <summary>
    /// Parses <see cref="ILastLoginRange"/> calendar days into a <see cref="UtcDateTimeRange"/>.
    /// </summary>
    public static UtcDateTimeRange ToLastLoginRange(this ILastLoginRange request) =>
        ToUtcDateTimeRange(request.LastLoginFrom, request.LastLoginTo);

    private static UtcDateTimeRange ToUtcDateTimeRange(string? from, string? to) =>
        new(UtcCalendarDay.InclusiveStartUtc(from), UtcCalendarDay.ExclusiveEndUtc(to));
}
