namespace Endatix.Api.Common;

/// <summary>
/// Paged list request with an optional search term.
/// </summary>
public interface ISearchablePagedRequest : IPagedRequest
{
    /// <summary>
    /// Optional free-text search filter.
    /// </summary>
    string? Search { get; set; }
}
