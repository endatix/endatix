using Endatix.Framework.Logging;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Data.Logging;

/// <summary>
/// Source-generated logging for data seeding startup.
/// </summary>
internal static partial class DataSeedingLoggerExtensions
{
    [LoggerMessage(
        EventId = EndatixEventIds.Seeding.Started,
        Level = LogLevel.Information,
        Message = "Seeding initial application data")]
    public static partial void LogSeedingStarted(this ILogger logger);

    [LoggerMessage(
        EventId = EndatixEventIds.Seeding.Completed,
        Level = LogLevel.Information,
        Message = "Initial data seeded successfully")]
    public static partial void LogSeedingCompleted(this ILogger logger);

    [LoggerMessage(
        EventId = EndatixEventIds.Seeding.SampleDataStarted,
        Level = LogLevel.Information,
        Message = "Seeding sample data")]
    public static partial void LogSampleDataSeedingStarted(this ILogger logger);

    [LoggerMessage(
        EventId = EndatixEventIds.Seeding.SampleDataSeeded,
        Level = LogLevel.Information,
        Message = "🌱 Sample data seeded. Took: {ElapsedMs} ms.")]
    public static partial void LogSampleDataSeeded(this ILogger logger, double elapsedMs);

    [LoggerMessage(
        EventId = EndatixEventIds.Seeding.SampleDataFailed,
        Level = LogLevel.Error,
        Message = "An error occurred while seeding sample data. Data seeding aborted.")]
    public static partial void LogSampleDataSeedingFailed(this ILogger logger, Exception exception);
}
