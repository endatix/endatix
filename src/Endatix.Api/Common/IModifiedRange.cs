namespace Endatix.Api.Common;

/// <summary>
/// Inclusive UTC calendar day bounds for <c>ModifiedAt</c> (<c>YYYY-MM-DD</c>).
/// </summary>
public interface IModifiedRange
{
    /// <summary>
    /// Inclusive modified-at UTC calendar day.
    /// </summary>
    string? ModifiedFrom { get; set; }

    /// <summary>
    /// Inclusive modified-at UTC calendar day (API maps to exclusive next-day).
    /// </summary>
    string? ModifiedTo { get; set; }
}
