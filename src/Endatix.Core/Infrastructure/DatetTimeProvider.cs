using Endatix.Core.Abstractions;

namespace Endatix.Core.Infrastructure;

/// <summary>
/// Provides access to the current date and time in UTC
/// </summary>
public class DateTimeProvider : IDateTimeProvider
{
    /// <inheritdoc/>
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}