using Microsoft.Extensions.Logging;

namespace Endatix.Core.Infrastructure.Logging;

/// <summary>
/// Source-generated logging for <see cref="LoggingPipelineBehavior{TRequest, TResponse}"/>.
/// </summary>
internal static partial class LoggingPipelineLoggerExtensions
{
    [LoggerMessage(
        EventName = "RequestHandling",
        Level = LogLevel.Information,
        Message = "Handling {RequestName}")]
    public static partial void LogRequestHandling(this ILogger logger, string requestName);

    [LoggerMessage(
        EventName = "RequestProperty",
        Level = LogLevel.Information,
        Message = "Property {Property} : {Value}")]
    public static partial void LogRequestProperty(this ILogger logger, string property, object? value);

    [LoggerMessage(
        EventName = "RequestHandled",
        Level = LogLevel.Information,
        Message = "Handled {RequestName} with {Response} in {ElapsedMs} ms")]
    public static partial void LogRequestHandled(this ILogger logger, string requestName, object? response, long elapsedMs);
}
