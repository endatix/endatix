using Microsoft.Extensions.Logging;

namespace Endatix.Framework.Logging;

/// <summary>
/// Source-generated generic logging primitives for the Endatix shared kernel.
/// Domain-specific messages belong in collocated <c>*LoggerExtensions</c> types
/// in Infrastructure, Hosting, or modules.
/// </summary>
public static partial class EndatixLoggerExtensions
{
    /// <summary>
    /// Logs the start of a lifecycle operation.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    /// <param name="operation">The operation that started.</param>
    [LoggerMessage(
        EventId = EndatixEventIds.Lifecycle.OperationStarted,
        Level = LogLevel.Debug,
        Message = "{Operation} started")]
    public static partial void LogOperationStarted(this ILogger logger, string operation);

    /// <summary>
    /// Logs the completion of a lifecycle operation.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    /// <param name="operation">The operation that completed.</param>
    [LoggerMessage(
        EventId = EndatixEventIds.Lifecycle.OperationCompleted,
        Level = LogLevel.Debug,
        Message = "{Operation} completed successfully")]
    public static partial void LogOperationCompleted(this ILogger logger, string operation);

    /// <summary>
    /// Logs the skipping of a lifecycle operation.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    /// <param name="operation">The operation that was skipped.</param>
    /// <param name="reason">The reason the operation was skipped.</param>
    [LoggerMessage(
        EventId = EndatixEventIds.Lifecycle.OperationSkipped,
        Level = LogLevel.Debug,
        Message = "{Operation} skipped: {Reason}")]
    public static partial void LogOperationSkipped(this ILogger logger, string operation, string reason);

    /// <summary>
    /// Logs the failure of a lifecycle operation.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="operation">The operation that failed.</param>
    [LoggerMessage(
        EventId = EndatixEventIds.Lifecycle.OperationFailed,
        Level = LogLevel.Error,
        Message = "{Operation} failed")]
    public static partial void LogOperationFailed(this ILogger logger, Exception exception, string operation);
}
