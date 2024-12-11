namespace Endatix.Api.Common;

/// <summary>
/// Common interface to handle request that support paging. Use this for every request that should handle paging.
/// </summary>
public interface IPagedRequest
{
    /// <summary>
    /// The number of the page
    /// </summary>
    int? Page { get; set; }

    /// <summary>
    /// The number of items to take.
    /// </summary>
    int? PageSize { get; set; }
}
