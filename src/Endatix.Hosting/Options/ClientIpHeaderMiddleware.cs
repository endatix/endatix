using Microsoft.AspNetCore.Http;

namespace Endatix.Hosting.Options;

/// <summary>
/// Leaves FastEndpoints a single <c>X-Forwarded-For</c> value equal to <c>RemoteIpAddress</c>,
/// which forwarded-headers middleware has already resolved.
/// </summary>
internal sealed class ClientIpHeaderMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        Normalize(context);
        return next(context);
    }

    internal static void Normalize(HttpContext context)
    {
        context.Request.Headers.Remove("X-Azure-ClientIP");
        context.Request.Headers.Remove("CF-Connecting-IP");
        context.Request.Headers.Remove("True-Client-IP");
        context.Request.Headers.Remove("X-Real-IP");

        var remote = context.Connection.RemoteIpAddress;
        if (remote is null)
        {
            context.Request.Headers.Remove("X-Forwarded-For");
            return;
        }

        context.Request.Headers["X-Forwarded-For"] = remote.ToString();
    }
}
