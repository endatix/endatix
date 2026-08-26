namespace Endatix.Api.Common;

/// <summary>
/// Inclusive UTC calendar day bounds for <c>CompletedAt</c> (<c>YYYY-MM-DD</c>).
/// </summary>
public interface ICompletedRange
{
    /// <summary>
    /// Inclusive completed-at UTC calendar day.
    /// </summary>
    string? CompletedFrom { get; set; }

    /// <summary>
    /// Inclusive completed-at UTC calendar day (API maps to exclusive next-day).
    /// </summary>
    string? CompletedTo { get; set; }
}
