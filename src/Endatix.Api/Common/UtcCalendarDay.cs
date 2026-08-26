using System.Globalization;

namespace Endatix.Api.Common;

/// <summary>
/// Parses UTC calendar days (<c>YYYY-MM-DD</c>) for list/export date bounds.
/// Inclusive From is start of day; inclusive To becomes exclusive start of next day.
/// </summary>
public static class UtcCalendarDay
{
    public const string Format = "yyyy-MM-dd";

    /// <summary>
    /// Tries to parse a UTC calendar date string.
    /// </summary>
    public static bool TryParse(string? value, out DateOnly day)
    {
        day = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DateOnly.TryParseExact(
            value.Trim(),
            Format,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out day);
    }

    /// <summary>
    /// Inclusive start of the UTC calendar day, or null when <paramref name="value"/> is empty/invalid.
    /// </summary>
    public static DateTime? InclusiveStartUtc(string? value)
    {
        if (!TryParse(value, out DateOnly day))
        {
            return null;
        }

        return DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    }

    /// <summary>
    /// Exclusive end of the UTC calendar day (start of next day), or null when empty/invalid.
    /// Clamps <see cref="DateOnly.MaxValue"/> to <see cref="DateTime.MaxValue"/>.
    /// </summary>
    public static DateTime? ExclusiveEndUtc(string? value)
    {
        if (!TryParse(value, out DateOnly day))
        {
            return null;
        }

        // DateOnly.MaxValue has no next day; clamp instead of AddDays(1) overflow.
        if (day == DateOnly.MaxValue)
        {
            return DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
        }

        return DateTime.SpecifyKind(day.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    }

    /// <summary>
    /// True when either bound is missing/invalid, or <paramref name="from"/> is on or before <paramref name="to"/>.
    /// </summary>
    public static bool IsFromOnOrBeforeTo(string? from, string? to)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return true;
        }

        if (!TryParse(from, out DateOnly fromDay) || !TryParse(to, out DateOnly toDay))
        {
            return true;
        }

        return fromDay <= toDay;
    }
}
