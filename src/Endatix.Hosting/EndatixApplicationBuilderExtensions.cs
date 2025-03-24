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
    /// Configures the application with Endatix middleware using a builder.
    /// </summary>
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
    /// Configures the application with Endatix middleware using the provided options.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configure">A delegate to configure middleware options.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatix(this IApplicationBuilder app, Action<EndatixMiddlewareOptions> configure)
    {
        Guard.Against.Null(app, nameof(app));

        // Create options with defaults
        var options = new EndatixMiddlewareOptions();

        // Apply custom configuration
        configure(options);

        // Create middleware builder and apply configuration based on options
        var builder = new EndatixMiddlewareBuilder(app);

        if (options.UseExceptionHandler)
        {
            builder.UseExceptionHandler();
        }

        if (options.UseSecurity)
        {
            builder.UseSecurity();
        }

        if (options.UseMultitenancy)
        {
            builder.UseMultitenancy();
        }

        if (options.UseHsts)
        {
            builder.UseHsts();
        }

        if (options.UseHttpsRedirection)
        {
            builder.UseHttpsRedirection();
        }

        if (options.UseApi)
        {
            builder.UseApi(apiOptions => 
            {
                apiOptions.UseExceptionHandler = options.ApiOptions.UseExceptionHandler;
                apiOptions.ExceptionHandlerPath = options.ApiOptions.ExceptionHandlerPath;
                apiOptions.UseSwagger = options.ApiOptions.UseSwagger;
                apiOptions.EnableSwaggerInProduction = options.ApiOptions.EnableSwaggerInProduction;
                apiOptions.SwaggerPath = options.ApiOptions.SwaggerPath;
                apiOptions.ConfigureOpenApiDocument = options.ApiOptions.ConfigureOpenApiDocument;
                apiOptions.ConfigureSwaggerUi = options.ApiOptions.ConfigureSwaggerUi;
                apiOptions.UseCors = options.ApiOptions.UseCors;
                apiOptions.UseVersioning = options.ApiOptions.UseVersioning;
                apiOptions.VersioningPrefix = options.ApiOptions.VersioningPrefix;
                apiOptions.RoutePrefix = options.ApiOptions.RoutePrefix;
                apiOptions.ConfigureFastEndpoints = options.ApiOptions.ConfigureFastEndpoints;
            });
        }

        if (options.UseHealthChecks)
        {
            builder.UseHealthChecks(options.HealthCheckPath);
        }

        options.ConfigureAdditionalMiddleware?.Invoke(app);

        return app;
    }

    /// <summary>
    /// Configures all Endatix middleware components with default settings and then applies custom configuration.
    /// This hybrid approach allows you to start with sensible defaults and then customize specific middleware aspects.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configureAction">Action to configure middleware after defaults are applied.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatixWithDefaults(
        this IApplicationBuilder app,
        Action<EndatixMiddlewareBuilder> configureAction)
    {
        Guard.Against.Null(app);
        Guard.Against.Null(configureAction);

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

        // Use the dedicated health checks middleware builder to avoid code duplication
        var builder = new Builders.EndatixHealthChecksMiddlewareBuilder(new EndatixMiddlewareBuilder(app), 
            app.ApplicationServices.GetService(typeof(ILoggerFactory)) is ILoggerFactory factory
                ? factory.CreateLogger("Endatix.Middleware")
                : null);
        
        builder.WithPath(path);
        builder.Apply(app);

        app.GetLogger()?.LogInformation("Health checks mapped to {Path}", path);
        return app;
    }

    private static ILogger? GetLogger(this IApplicationBuilder app)
    {
        return app.ApplicationServices.GetService(typeof(ILoggerFactory)) is ILoggerFactory factory
            ? factory.CreateLogger("Endatix.Middleware")
            : null;
    }
}
