using Ardalis.GuardClauses;
using Endatix.Hosting.Builders;
using Endatix.Hosting.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Endatix.Hosting;

/// <summary>
/// Extension methods for configuring the application middleware pipeline.
/// </summary>
public static class EndatixApplicationBuilderExtensions
{
    /// <summary>
    /// Configures the application with Endatix middleware using default settings.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The configured application builder.</returns>
    public static IApplicationBuilder UseEndatix(this IApplicationBuilder app)
    {
        Guard.Against.Null(app, nameof(app));

        app.GetLogger()?.LogInformation("Configuring Endatix middleware with default settings");

        new EndatixMiddlewareBuilder(app)
            .UseDefaults();

        app.GetLogger()?.LogInformation("Endatix middleware configured with default settings");
        return app;
    }

    /// <summary>
    /// Configures the application with a manually composed Endatix middleware pipeline.
    /// </summary>
    /// <remarks>
    /// This overload does not apply default middleware automatically. Use <see cref="UseEndatix(IApplicationBuilder)" />
    /// or <see cref="UseEndatix(IApplicationBuilder, Action{EndatixMiddlewareOptions})" /> for default middleware plus overrides.
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <param name="configure">A delegate to configure the middleware builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatix(this IApplicationBuilder app, Action<EndatixMiddlewareBuilder> configure)
    {
        app.GetLogger()?.LogInformation("Configuring Endatix middleware with custom settings");

        var builder = new EndatixMiddlewareBuilder(app);
        configure(builder);

        app.GetLogger()?.LogInformation("Endatix middleware configured with custom settings");
        return app;
    }

    /// <summary>
    /// Configures the application with Endatix middleware using default options, then applies the provided overrides.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configure">A delegate to configure middleware options.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatix(this IApplicationBuilder app, Action<EndatixMiddlewareOptions> configure)
    {
        Guard.Against.Null(app, nameof(app));

        var options = EndatixMiddlewareOptionsFactory.Create(app.ApplicationServices);
        configure(options);

        new EndatixMiddlewareBuilder(app)
            .UseOptions(options);

        return app;
    }

    /// <summary>
    /// Configures all Endatix middleware components with default settings and then applies custom configuration.
    /// This hybrid approach allows you to start with sensible defaults and then customize specific middleware aspects.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configureAction">Action to configure middleware after defaults are applied.</param>
    /// <returns>The application builder for chaining.</returns>
    [Obsolete("Use UseEndatix(options => ...) for defaults plus overrides, or UseEndatix(builder => ...) for manual composition.")]
    public static IApplicationBuilder UseEndatixWithDefaults(
        this IApplicationBuilder app,
        Action<EndatixMiddlewareBuilder> configureAction)
    {
        Guard.Against.Null(app);
        Guard.Against.Null(configureAction);

        app.GetLogger()?.LogWarning(
            "{Method} is obsolete. Use UseEndatix(options => ...) for defaults plus overrides, or UseEndatix(builder => ...) for manual composition.",
            nameof(UseEndatixWithDefaults));

        var middlewareBuilder = new EndatixMiddlewareBuilder(app);

        // First apply defaults
        middlewareBuilder.UseDefaults();

        // Then apply custom configuration
        configureAction(middlewareBuilder);

        return app;
    }

    /// <summary>
    /// Maps health check endpoints at the specified path.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="path">The path where health checks will be exposed.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseHealthChecks(this IApplicationBuilder app, string path = "/health")
    {
        Guard.Against.Null(app, nameof(app));

        new EndatixMiddlewareBuilder(app).UseHealthChecks(path);
        return app;
    }

    private static ILogger? GetLogger(this IApplicationBuilder app)
    {
        return app.ApplicationServices.GetService(typeof(ILoggerFactory)) is ILoggerFactory factory
            ? factory.CreateLogger("Endatix.Middleware")
            : null;
    }
}
