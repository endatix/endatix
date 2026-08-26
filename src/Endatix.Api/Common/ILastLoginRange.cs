namespace Endatix.Api.Common;

/// <summary>
/// Inclusive UTC calendar day bounds for <c>LastLoginAt</c> (<c>YYYY-MM-DD</c>).
/// </summary>
public interface ILastLoginRange
{
    /// <summary>
    /// Inclusive last-login UTC calendar day.
    /// </summary>
    string? LastLoginFrom { get; set; }

    /// <summary>
    /// Inclusive last-login UTC calendar day (API maps to exclusive next-day).
    /// </summary>
    string? LastLoginTo { get; set; }
}
