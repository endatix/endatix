namespace Endatix.Api.Common;

/// <summary>
/// Paged list request capability.
/// </summary>
public interface IPagedRequest
{
    /// <summary>
    /// The page number.
    /// </summary>
    int? Page { get; set; }

    /// <summary>
    /// The number of items per page.
    /// </summary>
    int? PageSize { get; set; }
}
