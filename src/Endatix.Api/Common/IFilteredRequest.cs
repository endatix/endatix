namespace Endatix.Api.Common;

/// <summary>
/// Common interface to handle request that support filtering. Use this for every request that should handle filtering.
/// </summary>
public interface IFilteredRequest
{
    /// <summary>
    /// The filter expressions
    /// </summary>
    IEnumerable<string>? Filter { get; set; }
}
