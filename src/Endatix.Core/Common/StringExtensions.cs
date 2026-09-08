namespace Endatix.Core.Common;

public static class StringExtensions
{
    /// <summary>
    /// Null when the value is missing or whitespace; otherwise the original string (not trimmed).
    /// </summary>
    public static string? NullIfWhiteSpace(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
