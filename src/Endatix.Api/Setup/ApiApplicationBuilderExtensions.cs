using System.Security.Claims;
using Endatix.Api.Builders;
using Endatix.Api.Infrastructure;
using Endatix.Framework.Serialization;
using Endatix.Infrastructure.Identity;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        var optionsProvider = app.ApplicationServices.GetService<IOptions<ApiOptions>>();
        var options = optionsProvider?.Value ?? new ApiOptions();
        
        logger?.LogInformation("Using API options with UseSwagger={UseSwagger}, SwaggerPath={SwaggerPath}", 
            options.UseSwagger,
            options.SwaggerPath);
            
        // Set up standard middleware pipeline with options from configuration
        ConfigureApiMiddleware(app, options);

        logger?.LogInformation("Endatix API middleware configured successfully");
        return app;
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

        // Get options from DI first - use IOptions not IOptionsSnapshot
        var optionsProvider = app.ApplicationServices.GetService<IOptions<ApiOptions>>();
        var options = optionsProvider?.Value ?? new ApiOptions();
        
        // Apply custom configuration on top of configuration-provided values
        configure(options);
        
        logger?.LogInformation("Using API options with UseSwagger={UseSwagger}, SwaggerPath={SwaggerPath}", 
            options.UseSwagger,
            options.SwaggerPath);

        // Set up middleware pipeline based on options
        ConfigureApiMiddleware(app, options);

        logger?.LogInformation("Endatix API middleware configured successfully with custom options");
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
            app.UseSwaggerGen();
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
        var loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("Endatix.Api.Setup");

        logger?.LogInformation("Configuring API endpoints in the application pipeline");

        // Get options from DI - use IOptions not IOptionsSnapshot
        var optionsProvider = app.ApplicationServices.GetService<IOptions<ApiOptions>>();
        var options = optionsProvider?.Value ?? new ApiOptions();
        
        logger?.LogInformation("Using API options with UseSwagger={UseSwagger}, SwaggerPath={SwaggerPath}",
            options.UseSwagger,
            options.SwaggerPath);
            
        // Apply middleware based on options from configuration
        ConfigureApiMiddleware(app, options);

        logger?.LogInformation("API endpoints configured in the application pipeline");
        return app;
    }

    /// <summary>
    /// Configures the application to use API endpoints with custom configuration.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configureApi">A delegate to configure the API options.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseApiEndpoints(this IApplicationBuilder app, Action<ApiOptions> configureApi)
    {
        var loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("Endatix.Api.Setup");

        logger?.LogInformation("Configuring API endpoints in the application pipeline with custom options");

        // Get options from DI first - use IOptions not IOptionsSnapshot
        var optionsProvider = app.ApplicationServices.GetService<IOptions<ApiOptions>>();
        var options = optionsProvider?.Value ?? new ApiOptions();
        
        // Apply custom configuration on top of configuration-provided values
        configureApi(options);
        
        logger?.LogInformation("Using API options with UseSwagger={UseSwagger}, SwaggerPath={SwaggerPath}", 
            options.UseSwagger,
            options.SwaggerPath);

        // Apply middleware based on options
        ConfigureApiMiddleware(app, options);

        logger?.LogInformation("API endpoints configured in the application pipeline with custom options");
        return app;
    }
}


