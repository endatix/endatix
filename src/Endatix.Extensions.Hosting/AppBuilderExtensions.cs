using Endatix.Framework.Hosting;
using Endatix.Setup;
using Microsoft.AspNetCore.Builder;
using Serilog;

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

        app.UseAuthentication()
            .UseAuthorization();

        app.UseSerilogRequestLogging();

        app.UseHttpsRedirection();

        app.ApplyDbMigrations();

        var middleware = new EndatixMiddleware(app);

        return middleware;
    }
}
