using System.Globalization;

namespace Endatix.Api.Common;

/// <summary>
/// Utility parsing helpers for endpoint request models.
/// </summary>
public static class StringParsingExtensions
{
    /// <summary>
    /// Tries to parse a string into <see cref="long"/> using invariant culture.
    /// Works with null values without throwing an exception.
    /// </summary>
    public static bool TryParseToLong(this string? value, out long parsed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = default;
            return false;
        }

        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
    }

    /// <summary>
    /// Parses a nullable string into <see cref="long"/> when the value is a valid integer; otherwise returns null.
    /// </summary>
    public static long? ParseToLong(this string? value)
    {
        return value.TryParseToLong(out var parsed)
            ? parsed
            : null;
    }
}
