using Endatix.Api.Builders;
using Endatix.Hosting.Builders;
using Endatix.Hosting.Options;
using Microsoft.AspNetCore.Builder;
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
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatix(this IApplicationBuilder app)
    {
        app.GetLogger()?.LogInformation("Configuring Endatix middleware with default settings");

        new EndatixMiddlewareBuilder(app).UseDefaults();

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
    /// Configures the application with customized Endatix middleware using options.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configure">A delegate to configure middleware options.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatix(this IApplicationBuilder app, Action<EndatixMiddlewareOptions> configure)
    {
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
            // Configure API using the ApiOptions from EndatixMiddlewareOptions
            builder.UseApi(apiOpt =>
            {
                // Copy properties from ApiOptions to the delegate parameter
                apiOpt.UseExceptionHandler = options.ApiOptions.UseExceptionHandler;
                apiOpt.ExceptionHandlerPath = options.ApiOptions.ExceptionHandlerPath;
                apiOpt.UseSwagger = options.ApiOptions.UseSwagger;
                apiOpt.EnableSwaggerInProduction = options.ApiOptions.EnableSwaggerInProduction;
                apiOpt.SwaggerPath = options.ApiOptions.SwaggerPath;
                apiOpt.UseCors = options.ApiOptions.UseCors;
                apiOpt.UseVersioning = options.ApiOptions.UseVersioning;
                apiOpt.VersioningPrefix = options.ApiOptions.VersioningPrefix;
                apiOpt.RoutePrefix = options.ApiOptions.RoutePrefix;
                apiOpt.ConfigureFastEndpoints = options.ApiOptions.ConfigureFastEndpoints;
                apiOpt.ConfigureOpenApiDocument = options.ApiOptions.ConfigureOpenApiDocument;
                apiOpt.ConfigureSwaggerUi = options.ApiOptions.ConfigureSwaggerUi;
            });
        }

        // Apply any additional middleware
        options.ConfigureAdditionalMiddleware?.Invoke(app);

        return app;
    }

    private static ILogger? GetLogger(this IApplicationBuilder app)
    {
        return app.ApplicationServices.GetService(typeof(ILoggerFactory)) is ILoggerFactory factory
            ? factory.CreateLogger("Endatix.Middleware")
            : null;
    }
}
