using Endatix.Framework.Logging;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.ReCaptcha;

/// <summary>
/// Source-generated logging for reCAPTCHA token verification.
/// Expected visitor outcomes log at Information; problems with our configuration log at Warning
/// under their own EventIds so they can be alerted on.
/// </summary>
internal static partial class ReCaptchaLoggerExtensions
{
    [LoggerMessage(
        EventId = EndatixEventIds.ReCaptcha.TokenRejected,
        Level = LogLevel.Information,
        Message = "reCAPTCHA siteverify rejected the token. Action: {Action}. ErrorCodes: {ErrorCodes}")]
    public static partial void LogReCaptchaTokenRejected(this ILogger logger, string action, string errorCodes);

    [LoggerMessage(
        EventId = EndatixEventIds.ReCaptcha.TokenRejectedConfigurationError,
        Level = LogLevel.Warning,
        Message = "reCAPTCHA siteverify rejected the request because of a configuration error. Check the secret key. Action: {Action}. ErrorCodes: {ErrorCodes}")]
    public static partial void LogReCaptchaConfigurationError(this ILogger logger, string action, string errorCodes);

    [LoggerMessage(
        EventId = EndatixEventIds.ReCaptcha.ScoreTooLow,
        Level = LogLevel.Information,
        Message = "reCAPTCHA score {Score} is below minimum {MinimumScore}. Action: {Action}")]
    public static partial void LogReCaptchaScoreTooLow(this ILogger logger, double score, double minimumScore, string action);

    [LoggerMessage(
        EventId = EndatixEventIds.ReCaptcha.ActionMissing,
        Level = LogLevel.Warning,
        Message = "reCAPTCHA siteverify accepted a token without an action. Check that the site key is a v3 key. Hostname: {Hostname}")]
    public static partial void LogReCaptchaActionMissing(this ILogger logger, string? hostname);
}
