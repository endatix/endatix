using Endatix.Framework.Hosting;
using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.Events;

namespace Endatix.Setup;

/// <summary>
/// Provides extension methods for configuring Endatix-specific middleware in the application pipeline.
/// </summary>
public static class AppBuilderExtensions
{
    /// <summary>
    /// Adds Endatix-specific middleware to the application's request processing pipeline.
    /// </summary>
    /// <param name="app">The <see cref="WebApplication"/> instance to configure.</param>
    /// <returns>An <see cref="IEndatixMiddleware"/> instance representing the configured middleware.</returns>
    public static IEndatixMiddleware UseEndatixMiddleware(this WebApplication app)
    {
        app.UseHsts();
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (httpContext, elapsed, ex) =>
            {
                if (httpContext.Request.Path.StartsWithSegments("/healthz"))
                {
                    return LogEventLevel.Verbose;
                }

                return LogEventLevel.Information;
            };
        });

        app.SeedInitialUser();
        app.ApplyDbMigrations();

        var middleware = new EndatixMiddleware(app);

        return middleware;
    }
}
