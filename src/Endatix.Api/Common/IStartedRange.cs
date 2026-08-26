namespace Endatix.Api.Common;

/// <summary>
/// Inclusive UTC calendar day bounds for <c>StartedAt</c> (<c>YYYY-MM-DD</c>).
/// </summary>
public interface IStartedRange
{
    /// <summary>
    /// Inclusive started-at UTC calendar day.
    /// </summary>
    string? StartedFrom { get; set; }

    /// <summary>
    /// Inclusive started-at UTC calendar day (API maps to exclusive next-day).
    /// </summary>
    string? StartedTo { get; set; }
}
