using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Endatix.Api.Infrastructure;

/// <summary>
/// Last-resort safety net: logs an unhandled exception and writes an opaque 500.
/// </summary>
/// <remarks>
/// Deliberately not a status mapper. Expected failures are modelled as <c>Result</c> and get their
/// status from <c>ToProblem</c>; anything reaching here - including a domain exception that opted into
/// <c>IEndUserSafeError</c> - is a handler that failed to convert, which is a defect. Answering it with
/// a tidy 4xx would hide the defect and make throwing more ergonomic than returning a Result.
/// </remarks>
public sealed class EndatixExceptionHandler(ILogger<EndatixExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception for {Method} {Path}",
            RequestLogSanitizer.Sanitize(httpContext.Request.Method),
            RequestLogSanitizer.Sanitize(httpContext.Request.Path.Value));

        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        var problem = EndatixProblemDetails.ForUnhandledException(httpContext);
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken);

        return true;
    }
}
