using System.Diagnostics;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Endatix.Api.Infrastructure.ResultExtensions;

namespace Endatix.Api.Infrastructure;

/// <summary>
/// Builds the canonical RFC7807 ProblemDetails envelope used by handler <c>ToProblem</c>,
/// FastEndpoints FluentValidation, unhandled exceptions, and streaming export errors.
/// </summary>
public static class EndatixProblemDetails
{
    private static IHttpContextAccessor? _httpContextAccessor;
    private static ILoggerFactory? _loggerFactory;

    /// <summary>
    /// Wires the request-scoped <see cref="IHttpContextAccessor"/> so static helpers
    /// (e.g. <c>ToProblem</c>) can enrich <c>instance</c> / <c>traceId</c>.
    /// </summary>
    public static void Configure(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Creates ProblemDetails from FastEndpoints / FluentValidation failures.
    /// Sets <c>application/problem+json</c> on the response when the body has not started.
    /// </summary>
    public static ProblemDetails FromValidationFailures(
        List<ValidationFailure> failures,
        HttpContext httpContext,
        int statusCode)
    {
        ArgumentNullException.ThrowIfNull(failures);
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!httpContext.Response.HasStarted)
        {
            httpContext.Response.ContentType = "application/problem+json";
        }

        var fields = failures
            .Where(failure => !string.IsNullOrWhiteSpace(failure.PropertyName))
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray(),
                StringComparer.Ordinal);

        var messages = failures
            .Select(failure => failure.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToList();

        var errorCode = failures
            .Select(failure => failure.ErrorCode)
            .FirstOrDefault(code => !string.IsNullOrWhiteSpace(code));

        return Create(
            statusCode: statusCode,
            title: null,
            detail: string.Join('\n', messages),
            httpContext: httpContext,
            errorCode: errorCode,
            fields: fields.Count > 0 ? fields : null);
    }

    /// <summary>
    /// Generic unhandled-exception ProblemDetails. Never includes exception text.
    /// </summary>
    public static ProblemDetails ForUnhandledException(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return Create(
            statusCode: StatusCodes.Status500InternalServerError,
            title: ResultTitles.INTERNAL_SERVER_ERROR,
            detail: ResultTitles.INTERNAL_SERVER_ERROR,
            httpContext: httpContext);
    }

    /// <summary>
    /// Builds ProblemDetails for an arbitrary status/detail (e.g. export stream errors).
    /// Uses the current request context when <paramref name="httpContext"/> is null.
    /// </summary>
    public static ProblemDetails Create(
        int statusCode,
        string? title,
        string? detail,
        HttpContext? httpContext = null,
        string? errorCode = null,
        IReadOnlyDictionary<string, string[]>? fields = null)
    {
        var resolvedContext = httpContext ?? _httpContextAccessor?.HttpContext;
        var resolvedTitle = title ?? TitleForStatus(statusCode);
        var resolvedDetail = string.IsNullOrWhiteSpace(detail) ? resolvedTitle : detail;

        // A 5xx body must never carry handler- or exception-derived text. Handlers routinely
        // wrap `ex.Message` into Result.Error(...) (DB/EF text, file paths, provider errors),
        // which ToProblem would otherwise echo to the caller. The generic title goes to the
        // client; the real text is logged so operators keep the diagnostics.
        if (statusCode >= StatusCodes.Status500InternalServerError
            && !string.Equals(resolvedDetail, resolvedTitle, StringComparison.Ordinal))
        {
            LogSuppressedDetail(resolvedContext, statusCode, resolvedDetail);
            resolvedDetail = resolvedTitle;
        }

        var problem = new ProblemDetails
        {
            Type = TypeForStatus(statusCode),
            Title = resolvedTitle,
            Status = statusCode,
            Detail = resolvedDetail,
            Instance = resolvedContext?.Request.Path.Value,
        };

        var traceId = Activity.Current?.Id ?? resolvedContext?.TraceIdentifier;
        if (!string.IsNullOrWhiteSpace(traceId))
        {
            problem.Extensions["traceId"] = traceId;
        }

        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            problem.Extensions["errorCode"] = errorCode;
        }

        if (fields is { Count: > 0 })
        {
            problem.Extensions["fields"] = fields is Dictionary<string, string[]> dictionary
                ? dictionary
                : fields.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }

        return problem;
    }

    /// <summary>
    /// Records the client-suppressed 5xx detail so the diagnostic is not lost. Correlate with
    /// the response via <c>traceId</c>.
    /// </summary>
    private static void LogSuppressedDetail(HttpContext? httpContext, int statusCode, string detail)
    {
        ILoggerFactory? loggerFactory = null;
        if (httpContext?.RequestServices is not null)
        {
            loggerFactory = httpContext.RequestServices.GetService<ILoggerFactory>();
        }

        loggerFactory ??= _loggerFactory;
        var logger = loggerFactory?.CreateLogger(typeof(EndatixProblemDetails));
        if (logger is null)
        {
            return;
        }

        logger.LogError(
            "Suppressed {StatusCode} problem detail for {Method} {Path} (traceId {TraceId}): {SuppressedDetail}",
            statusCode,
            RequestLogSanitizer.Sanitize(httpContext?.Request.Method),
            RequestLogSanitizer.Sanitize(httpContext?.Request.Path.Value),
            Activity.Current?.Id ?? httpContext?.TraceIdentifier,
            RequestLogSanitizer.Sanitize(detail));
    }

    internal static string TypeForStatus(int statusCode) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest => "https://www.rfc-editor.org/rfc/rfc9110.html#name-400-bad-request",
            StatusCodes.Status401Unauthorized => "https://www.rfc-editor.org/rfc/rfc9110.html#name-401-unauthorized",
            StatusCodes.Status403Forbidden => "https://www.rfc-editor.org/rfc/rfc9110.html#name-403-forbidden",
            StatusCodes.Status404NotFound => "https://www.rfc-editor.org/rfc/rfc9110.html#name-404-not-found",
            StatusCodes.Status409Conflict => "https://www.rfc-editor.org/rfc/rfc9110.html#name-409-conflict",
            StatusCodes.Status429TooManyRequests => "https://www.rfc-editor.org/rfc/rfc6585.html#section-4",
            StatusCodes.Status500InternalServerError => "https://www.rfc-editor.org/rfc/rfc9110.html#name-500-internal-server-error",
            StatusCodes.Status503ServiceUnavailable => "https://www.rfc-editor.org/rfc/rfc9110.html#name-503-service-unavailable",
            _ => "about:blank",
        };

    internal static string TitleForStatus(int statusCode) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest => ResultTitles.BAD_REQUEST,
            StatusCodes.Status401Unauthorized => ResultTitles.UNAUTHORIZED,
            StatusCodes.Status403Forbidden => ResultTitles.FORBIDDEN,
            StatusCodes.Status404NotFound => ResultTitles.NOT_FOUND,
            StatusCodes.Status409Conflict => ResultTitles.CONFLICT,
            StatusCodes.Status429TooManyRequests => ResultTitles.TOO_MANY_REQUESTS,
            StatusCodes.Status503ServiceUnavailable => ResultTitles.SERVICE_UNAVAILABLE,
            >= StatusCodes.Status400BadRequest and < StatusCodes.Status500InternalServerError
                => ResultTitles.BAD_REQUEST,
            _ => ResultTitles.INTERNAL_SERVER_ERROR,
        };
}

/// <summary>
/// Strips CR/LF from request-derived values before they are written to logs (CodeQL log injection).
/// </summary>
internal static class RequestLogSanitizer
{
    public static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace('\r', ' ').Replace('\n', ' ');
    }
}
