using System.Security.Claims;
using Endatix.Api.Builders;
using Endatix.Framework.Serialization;
using Endatix.Infrastructure.Identity;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Api.Setup;

/// <summary>
/// Extension methods for configuring API endpoints in the application pipeline.
/// </summary>
public static class ApiApplicationBuilderExtensions
{
    /// <summary>
    /// Configures the application to use Endatix API middleware.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatixApi(this IApplicationBuilder app)
    {
        var loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("Endatix.Api.Setup");

        logger?.LogInformation("Configuring Endatix API middleware");

        var options = GetApiOptions(app);

        logger?.LogInformation("Using API options with UseSwagger={UseSwagger}, SwaggerPath={SwaggerPath}",
            options.UseSwagger,
            options.SwaggerPath);

        var result = app.UseEndatixApi(options);
        logger?.LogInformation("Endatix API middleware configured successfully");
        return result;
    }

    /// <summary>
    /// Configures the application to use Endatix API middleware with custom options.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configure">A delegate to configure middleware options.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatixApi(this IApplicationBuilder app, Action<ApiOptions> configure)
    {
        var loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("Endatix.Api.Setup");

        logger?.LogInformation("Configuring Endatix API middleware with custom options");

        var options = GetApiOptions(app);

        // Apply custom configuration on top of configuration-provided values
        configure(options);

        logger?.LogInformation("Using API options with UseSwagger={UseSwagger}, SwaggerPath={SwaggerPath}",
            options.UseSwagger,
            options.SwaggerPath);

        var result = app.UseEndatixApi(options);
        logger?.LogInformation("Endatix API middleware configured successfully with custom options");
        return result;
    }

    /// <summary>
    /// Configures the application to use Endatix API middleware with resolved options.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="options">The resolved API middleware options.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatixApi(this IApplicationBuilder app, ApiOptions options)
    {
        ConfigureApiMiddleware(app, options);
        return app;
    }

    /// <summary>
    /// Internal method to configure the API middleware pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="options">The middleware options.</param>
    private static void ConfigureApiMiddleware(IApplicationBuilder app, ApiOptions options)
    {
        // Apply exception handling middleware if enabled
        if (options.UseExceptionHandler)
        {
            app.UseExceptionHandler(options.ExceptionHandlerPath);
        }

        // Apply FastEndpoints middleware with configuration
        app.UseFastEndpoints(c =>
        {
            // Apply versioning configuration
            c.Versioning.Prefix = options.VersioningPrefix;
            c.Endpoints.RoutePrefix = options.RoutePrefix;

            // Apply serializer configuration
            c.Serializer.Options.Converters.Add(new LongToStringConverter());

            // Apply security configuration
            c.Security.RoleClaimType = ClaimTypes.Role;
            c.Security.PermissionsClaimType = ClaimNames.Permission;

            // Apply any custom configuration
            options.ConfigureFastEndpoints?.Invoke(c);
        });

        // Apply Swagger middleware if enabled
        if (options.UseSwagger)
        {
            app.UseSwaggerGen(
                options.ConfigureOpenApiDocument,
                swaggerUiSettings =>
                {
                    if (options.SwaggerPath is not null)
                    {
                        swaggerUiSettings.Path = options.SwaggerPath;
                    }

                    options.ConfigureSwaggerUi?.Invoke(swaggerUiSettings);
                });
        }

        // Apply CORS middleware if enabled
        if (options.UseCors)
        {
            app.UseCors();
        }
    }

    /// <summary>
    /// Configures the application to use API endpoints.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseApiEndpoints(this IApplicationBuilder app)
    {
        return app.UseEndatixApi();
    }

    /// <summary>
    /// Configures the application to use API endpoints with custom configuration.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configureApi">A delegate to configure the API options.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseApiEndpoints(this IApplicationBuilder app, Action<ApiOptions> configureApi)
    {
        return app.UseEndatixApi(configureApi);
    }

    private static ApiOptions GetApiOptions(IApplicationBuilder app)
    {
        var optionsProvider = app.ApplicationServices.GetService<IOptions<ApiOptions>>();
        return optionsProvider?.Value ?? new ApiOptions();
    }
}


