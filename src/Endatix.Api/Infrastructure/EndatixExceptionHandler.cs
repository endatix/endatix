using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Endatix.Api.Infrastructure;

/// <summary>
/// Writes the canonical ProblemDetails envelope for unhandled exceptions without leaking exception text.
/// </summary>
public sealed class EndatixExceptionHandler(ILogger<EndatixExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path.Value);

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
