namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Date/time helpers for integration assertions against provider storage precision.
/// </summary>
public static class DateTimeTestHelpers
{
    /// <summary>
    /// Aligns with PostgreSQL <c>timestamptz</c> / typical SQL datetime storage (1 µs = 10 ticks).
    /// </summary>
    public static DateTime TruncateToMicroseconds(DateTime value) =>
        new(value.Ticks - (value.Ticks % TimeSpan.TicksPerMicrosecond), value.Kind);
}
