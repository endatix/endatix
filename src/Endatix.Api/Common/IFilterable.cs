namespace Endatix.Api.Common;

/// <summary>
/// Open REST filter expression capability for list requests.
/// </summary>
public interface IFilterable
{
    /// <summary>
    /// Filter expressions in the form <c>field:operator:value</c>.
    /// </summary>
    IEnumerable<string>? Filter { get; set; }
}
