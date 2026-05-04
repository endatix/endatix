namespace Endatix.Core.Abstractions;

/// <summary>
/// Provides access to the current date and time
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>
    /// Gets the current date and time in UTC
    /// </summary>
    DateTimeOffset Now { get; }

    /// <summary>
    /// UTC instant for token and audit timestamps. Defaults to <see cref="Now"/> (always UTC).
    /// </summary>
    DateTimeOffset UtcNow => Now;
}