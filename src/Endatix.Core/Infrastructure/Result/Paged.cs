using System.Text.Json.Serialization;
using Ardalis.GuardClauses;

namespace Endatix.Core.Infrastructure.Result;

/// <summary>
/// Paged data.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
public class Paged<T> : IPagedData
{
    /// <summary>
    /// The minimum page number.
    /// </summary>
    public const int MIN_PAGE = 1;
    /// <summary>
    /// Initializes a new instance of the <see cref="Paged{T}"/> class.
    /// </summary>
    /// <param name="page">The page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="totalRecords">The total number of records.</param>
    /// <param name="totalPages">The total number of pages.</param>
    /// <param name="items">The items on the page.</param>
    public Paged(int page, int pageSize, long totalRecords, long totalPages, IReadOnlyList<T> items)
    {
        Guard.Against.NegativeOrZero(page);
        Guard.Against.NegativeOrZero(pageSize);
        Guard.Against.Negative(totalRecords);
        Guard.Against.Negative(totalPages);
        Guard.Against.Null(items);

        var expectedTotalPages = totalRecords > 0
            ? (totalRecords + pageSize - 1) / pageSize
            : 0;

        if (totalPages != expectedTotalPages)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalPages),
                $"TotalPages ({totalPages}) must be = Ceil(totalRecords ({totalRecords}) / pageSize ({pageSize})) = {expectedTotalPages}.");
        }

        if (totalPages > 0 && page > totalPages && items.Count > 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(page),
                $"Page ({page}) must be <= TotalPages ({totalPages}) when items are present.");
        }

        if (totalPages == 0 && page != MIN_PAGE)
        {
            throw new ArgumentOutOfRangeException(
                nameof(page),
                "Page must be 1 when TotalPages is 0.");
        }

        Page = page;
        PageSize = pageSize;
        TotalRecords = totalRecords;
        TotalPages = totalPages;
        Items = items;
    }

    /// <summary>
    /// The page number.
    /// </summary>
    [JsonInclude]
    public int Page { get; init; }

    /// <summary>
    /// The page size.
    /// </summary>
    [JsonInclude]
    public int PageSize { get; init; }

    /// <summary>
    /// The total number of records.
    /// </summary>
    [JsonInclude]
    public long TotalRecords { get; init; }

    /// <summary>
    /// The total number of pages.
    /// </summary>
    [JsonInclude]
    public long TotalPages { get; init; }

    /// <summary>
    /// The items on the page.
    /// </summary>
    [JsonInclude]
    public IReadOnlyList<T> Items { get; init; }

    /// <summary>
    /// Creates a new <see cref="Paged{T}"/> from a paged request.
    /// </summary>
    /// <param name="skip">The number of items to skip.</param>
    /// <param name="take">The number of items to take.</param>
    /// <param name="totalRecords">The total number of records.</param>
    /// <param name="items">The items on the page.</param>
    /// <returns>A new <see cref="Paged{T}"/>.</returns>
    public static Paged<T> FromPagedRequest(int skip, int take, long totalRecords, IReadOnlyList<T> items)
    {
        Guard.Against.Negative(skip);
        Guard.Against.NegativeOrZero(take);
        Guard.Against.Negative(totalRecords);
        Guard.Against.Null(items);

        if (totalRecords == 0)
        {
            return Empty(take);
        }

        var totalPages = (int)((totalRecords + take - 1) / take);

        var currentPage = (skip / take) + 1;
        if (currentPage > totalPages)
        {
            currentPage = totalPages;
        }

        return new Paged<T>(
            page: currentPage,
            pageSize: take,
            totalRecords: totalRecords,
            totalPages: totalPages,
            items: items);
    }

    /// <summary>
    /// Creates a new empty <see cref="Paged{T}"/>.
    /// </summary>
    /// <param name="pageSize">The page size.</param>
    /// <returns>A new empty <see cref="Paged{T}"/>.</returns>
    public static Paged<T> Empty(int pageSize) => new(
        page: MIN_PAGE,
        pageSize: pageSize,
        totalRecords: 0,
        totalPages: 0,
        items: []);
}
