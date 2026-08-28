using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Endatix.IntegrationTests.Infrastructure;

/// <summary>
/// Test-host-only route that throws, so the unhandled-exception contract can be proven through
/// the real middleware pipeline rather than by unit-testing the handler in isolation.
///
/// The middleware is appended <em>after</em> the application's own pipeline, which places it
/// downstream of <c>UseExceptionHandler()</c>. A throw here therefore unwinds into
/// <c>EndatixExceptionHandler</c> exactly as a genuine fault in an endpoint would.
/// </summary>
internal sealed class UnhandledExceptionRouteStartupFilter : IStartupFilter
{
    /// <summary>Unmatched by any real endpoint, so it falls through to the appended middleware.</summary>
    public const string Path = "/api/__integration-test__/throw";

    /// <summary>
    /// Stand-in for the kind of text that must never surface: connection strings, SQL, file
    /// paths. The test asserts this string is absent from the response body.
    /// </summary>
    public const string SensitiveMarker = "Server=db;Password=SENSITIVE-a7f3c1;";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);

        app.Use(async (HttpContext context, RequestDelegate continuation) =>
        {
            if (string.Equals(context.Request.Path.Value, Path, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Simulated unhandled fault. {SensitiveMarker}");
            }

            await continuation(context);
        });
    };
}
