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
    public Paged(int page, int pageSize, int totalRecords, int totalPages, IReadOnlyList<T> items)
    {
        Guard.Against.NegativeOrZero(page);
        Guard.Against.NegativeOrZero(pageSize);
        Guard.Against.Negative(totalRecords);
        Guard.Against.Negative(totalPages);
        Guard.Against.Null(items);

        var expectedTotalPages = totalRecords > 0
            ? CalculateTotalPages(totalRecords, pageSize)
            : 0;

        ValidateConstructorArguments(page, pageSize, totalRecords, totalPages, expectedTotalPages, items);

        Page = page;
        PageSize = pageSize;
        TotalRecords = totalRecords;
        TotalPages = totalPages;
        Items = items;
    }

    private static void ValidateConstructorArguments(
        int page,
        int pageSize,
        int totalRecords,
        int totalPages,
        int expectedTotalPages,
        IReadOnlyList<T> items)
    {
        if (totalPages != expectedTotalPages)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalPages),
                $"TotalPages ({totalPages}) must be = Ceil(totalRecords ({totalRecords}) / pageSize ({pageSize})) = {expectedTotalPages}.");
        }

        if (totalPages > 0 && (page < MIN_PAGE || page > totalPages))
        {
            throw new ArgumentOutOfRangeException(
                nameof(page),
                $"Page ({page}) must be between {MIN_PAGE} and TotalPages ({totalPages}).");
        }

        if (totalPages == 0 && page != MIN_PAGE)
        {
            throw new ArgumentOutOfRangeException(
                nameof(page),
                "Page must be 1 when TotalPages is 0.");
        }

        if (totalPages == 0 && items.Count != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(items),
                "Items must be empty when TotalPages is 0.");
        }

        if (totalPages > 0 && items.Count > pageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(items),
                $"Items count ({items.Count}) must be <= pageSize ({pageSize}).");
        }

        var remainingRecordsOnPage = totalPages > 0
            ? totalRecords - ((page - 1) * pageSize)
            : 0;

        if (totalPages > 0 && items.Count > remainingRecordsOnPage)
        {
            throw new ArgumentOutOfRangeException(
                nameof(items),
                $"Items count ({items.Count}) must be <= remaining records on page ({remainingRecordsOnPage}).");
        }
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
    public int TotalRecords { get; init; }

    /// <summary>
    /// The total number of pages.
    /// </summary>
    [JsonInclude]
    public int TotalPages { get; init; }

    /// <summary>
    /// The items on the page.
    /// </summary>
    [JsonInclude]
    public IReadOnlyList<T> Items { get; init; }

    /// <summary>
    /// Creates a new <see cref="Paged{T}"/> from page-based paging parameters.
    /// </summary>
    /// <param name="page">The page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="totalRecords">The total number of records.</param>
    /// <param name="items">The items on the page.</param>
    /// <returns>A new <see cref="Paged{T}"/>.</returns>
    public static Paged<T> FromPage(int page, int pageSize, int totalRecords, IReadOnlyList<T> items)
    {
        Guard.Against.NegativeOrZero(page);
        Guard.Against.NegativeOrZero(pageSize);
        Guard.Against.Negative(totalRecords);
        Guard.Against.Null(items);

        if (totalRecords == 0)
        {
            if (items.Count != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(items),
                    "Items must be empty when TotalRecords is 0.");
            }

            return Empty(pageSize);
        }

        var totalPages = CalculateTotalPages(totalRecords, pageSize);
        var currentPage = ResolvePage(page, pageSize, totalRecords);

        return new Paged<T>(
            page: currentPage,
            pageSize: pageSize,
            totalRecords: totalRecords,
            totalPages: totalPages,
            items: items);
    }

    /// <summary>
    /// Creates a new <see cref="Paged{T}"/> from a paged request.
    /// </summary>
    /// <param name="skip">The number of items to skip.</param>
    /// <param name="take">The number of items to take.</param>
    /// <param name="totalRecords">The total number of records.</param>
    /// <param name="items">The items on the page.</param>
    /// <returns>A new <see cref="Paged{T}"/>.</returns>
    public static Paged<T> FromSkipAndTake(int skip, int take, int totalRecords, IReadOnlyList<T> items)
    {
        Guard.Against.Negative(skip);
        Guard.Against.NegativeOrZero(take);
        Guard.Against.Negative(totalRecords);
        Guard.Against.Null(items);

        var page = totalRecords == 0
            ? MIN_PAGE
            : (skip / take) + 1;

        return FromPage(page, take, totalRecords, items);
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

    /// <summary>
    /// Resolves the page number to use for querying and response metadata,
    /// clamping to the last valid page when the requested page exceeds the total.
    /// </summary>
    /// <param name="page">The requested page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="totalRecords">The total number of records.</param>
    /// <returns>The page number to use for skip/take and <see cref="FromPage"/>.</returns>
    public static int ResolvePage(int page, int pageSize, int totalRecords)
    {
        Guard.Against.NegativeOrZero(page);
        Guard.Against.NegativeOrZero(pageSize);
        Guard.Against.Negative(totalRecords);

        if (totalRecords == 0)
        {
            return MIN_PAGE;
        }

        var totalPages = CalculateTotalPages(totalRecords, pageSize);
        return page > totalPages ? totalPages : page;
    }

    private static int CalculateTotalPages(int totalRecords, int pageSize) =>
        (int)((totalRecords + (long)pageSize - 1) / pageSize);
}
