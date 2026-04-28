using System.Text.Json.Serialization;

namespace Endatix.Core.Infrastructure.Result;

/// <summary>
/// Paged data.
/// </summary>
/// <typeparam name="T">The type of the items.</typeparam>
public class Paged<T> : IPagedData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Paged{T}"/> class.
    /// </summary>
    /// <param name="page">The page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="totalRecords">The total number of records.</param>
    /// <param name="totalPages">The total number of pages.</param>
    /// <param name="items">The items on the page.</param>
    public Paged(long page, long pageSize, long totalRecords, long totalPages, T items)
    {
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
    public long Page { get; init; }

    /// <summary>
    /// The page size.
    /// </summary>
    [JsonInclude]
    public long PageSize { get; init; }

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
    public T Items { get; init; }
}
