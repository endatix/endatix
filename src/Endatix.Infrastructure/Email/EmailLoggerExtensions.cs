using System.Net;
using Endatix.Framework.Logging;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Email;

/// <summary>
/// Source-generated logging for email provider responses.
/// </summary>
internal static partial class EmailLoggerExtensions
{
    [LoggerMessage(
        EventId = EndatixEventIds.Email.ProviderMessageSent,
        Level = LogLevel.Information,
        Message = "Sending {Provider} message with status code {StatusCode} and response: {Response}")]
    public static partial void LogEmailSent(this ILogger logger, string provider, HttpStatusCode statusCode, string response);

    [LoggerMessage(
        EventId = EndatixEventIds.Email.ProviderTemplateMessageSent,
        Level = LogLevel.Information,
        Message = "Sending {Provider} template message with status code {StatusCode} and response: {Response}")]
    public static partial void LogTemplateEmailSent(this ILogger logger, string provider, HttpStatusCode statusCode, string response);
}
