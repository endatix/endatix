namespace Endatix.Api.Common;

/// <summary>
/// Free-text search capability for list requests.
/// </summary>
public interface ISearchableRequest
{
    /// <summary>
    /// Optional free-text search term.
    /// </summary>
    string? Search { get; set; }
}
