namespace Endatix.Api.Common;

/// <summary>
/// Inclusive UTC calendar day bounds for <c>CreatedAt</c> (<c>YYYY-MM-DD</c>).
/// </summary>
public interface ICreatedRange
{
    /// <summary>
    /// Inclusive created-at UTC calendar day.
    /// </summary>
    string? CreatedFrom { get; set; }

    /// <summary>
    /// Inclusive created-at UTC calendar day (API maps to exclusive next-day).
    /// </summary>
    string? CreatedTo { get; set; }
}
