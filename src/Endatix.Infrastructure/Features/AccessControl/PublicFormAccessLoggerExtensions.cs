using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.AccessControl;

/// <summary>
/// Source-generated logging for refusals on a form's public runtime reads. Each line names the form only: never
/// the token or the data list asked for.
/// </summary>
internal static partial class PublicFormAccessLoggerExtensions
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Public data-list read refused for form {FormId}: the list is not within the form's reach")]
    public static partial void LogDataListReadRefused(this ILogger logger, long formId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Form access token refused for form {FormId}: {Reason}")]
    public static partial void LogFormAccessTokenRefused(this ILogger logger, long formId, string reason);
}
