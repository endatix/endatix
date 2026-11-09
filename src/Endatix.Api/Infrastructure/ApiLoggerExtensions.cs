using Endatix.Framework.Logging;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Endatix.Api.Infrastructure;

/// <summary>
/// Source-generated logging for API host concerns (CORS setup and the Slack token endpoint).
/// </summary>
internal static partial class ApiLoggerExtensions
{
    [LoggerMessage(
        EventId = EndatixEventIds.Api.CorsDefaultPolicy,
        Level = LogLevel.Debug,
        Message = "Default Cors Policy is: {@Policy}")]
    public static partial void LogCorsDefaultPolicy(this ILogger logger, CorsPolicy? policy);

    [LoggerMessage(
        EventId = EndatixEventIds.Api.CorsCredentialsIgnored,
        Level = LogLevel.Warning,
        Message = "Ignoring {Setting} and disallowing credentials. Details: {Details}")]
    public static partial void LogCorsCredentialsIgnored(this ILogger logger, string setting, string details);

    [LoggerMessage(
        EventId = EndatixEventIds.Api.SlackTokenReceived,
        Level = LogLevel.Information,
        Message = "Received slack token request (token redacted)")]
    public static partial void LogSlackTokenReceived(this ILogger logger);
}
