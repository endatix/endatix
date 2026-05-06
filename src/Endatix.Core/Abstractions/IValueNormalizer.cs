namespace Endatix.Core.Abstractions;

/// <summary>
/// Normalizes string values for stable comparisons (e.g. case-insensitive uniqueness).
/// </summary>
public interface IValueNormalizer
{
    /// <summary>
    /// Returns the normalized form of <paramref name="value"/>, or null when the input normalizes to none. Used for case-insensitive uniqueness.
    /// </summary>
    /// <param name="value">The value to normalize.</param>
    /// <returns>The normalized value, or null when the input normalizes to none.</returns>
    string? Normalize(string? value);
}
