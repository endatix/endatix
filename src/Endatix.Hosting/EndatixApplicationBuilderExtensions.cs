using Microsoft.AspNetCore.Builder;
using System.Threading.Tasks;
using System;
using Endatix.Hosting.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Endatix.Api.Setup;

namespace Endatix.Hosting;

/// <summary>
/// Extension methods for configuring Endatix middleware.
/// </summary>
public static class EndatixApplicationBuilderExtensions
{
    /// <summary>
    /// Configures the application with default Endatix middleware.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatix(this IApplicationBuilder app)
    {
        var logger = app.ApplicationServices.GetService<ILogger<string>>();
        logger?.LogInformation("Configuring Endatix middleware");

        // Create middleware builder and apply default configuration
        var builder = new EndatixMiddlewareBuilder(app);
        builder
            .UseDefaults()
            .UseApi();

        logger?.LogInformation("Endatix middleware configured successfully");
        return app;
    }

    /// <summary>
    /// Configures the application with customized Endatix middleware.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configure">A delegate to configure middleware using the builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatix(this IApplicationBuilder app, Action<EndatixMiddlewareBuilder> configure)
    {
        var logger = app.ApplicationServices.GetService<ILogger<string>>();
        logger?.LogInformation("Configuring Endatix middleware with builder");

        // Create middleware builder and apply custom configuration
        var builder = new EndatixMiddlewareBuilder(app);
        configure(builder);

        logger?.LogInformation("Endatix middleware configured successfully with builder");
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
        var logger = app.ApplicationServices.GetService<ILogger<string>>();
        logger?.LogInformation("Configuring Endatix middleware with options");

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
            if (options.ApiOptions != null)
            {
                builder.UseApi(options.ApiOptions);
            }
            else
            {
                builder.UseApi();
            }
        }

        // Apply any additional middleware
        options.ConfigureAdditionalMiddleware?.Invoke(app);

        logger?.LogInformation("Endatix middleware configured successfully with options");
        return app;
    }

    /// <summary>
    /// Configures the application to use Endatix API middleware.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatixApi(this IApplicationBuilder app)
    {
        var logger = app.ApplicationServices.GetService<ILogger<string>>();
        logger?.LogInformation("Configuring Endatix API");

        // Use the ApiApplicationBuilderExtensions implementation
        app.UseApiEndpoints();

        logger?.LogInformation("Endatix API configured successfully");
        return app;
    }

    /// <summary>
    /// Configures the application to use legacy Endatix API middleware for backward compatibility.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseLegacyEndatixApi(this IApplicationBuilder app)
    {
        var logger = app.ApplicationServices.GetService<ILogger<string>>();
        logger?.LogInformation("Configuring legacy Endatix API");

        // Create middleware builder and use legacy API setup
        var builder = new EndatixMiddlewareBuilder(app);
        builder.UseLegacyEndatixApi();

        logger?.LogInformation("Legacy Endatix API configured successfully");
        return app;
    }

    /// <summary>
    /// Configures the application to use Endatix API middleware with custom options.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configure">A delegate to configure API middleware options.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatixApi(this IApplicationBuilder app, Action<EndatixApiMiddlewareOptions> configure)
    {
        var logger = app.ApplicationServices.GetService<ILogger<string>>();
        logger?.LogInformation("Configuring Endatix API with custom options");

        // Use the ApiApplicationBuilderExtensions implementation
        app.UseApiEndpoints(builder =>
        {
            // Adapt the API middleware options to the API configuration builder
            // This maintains backward compatibility with the old API
            var options = new EndatixApiMiddlewareOptions();
            configure(options);

            // Apply options to builder
            if (options.UseSwagger)
            {
                builder.AddSwagger();
            }

            if (options.UseVersioning)
            {
                builder.AddVersioning();
            }
        });

        logger?.LogInformation("Endatix API configured successfully with custom options");
        return app;
    }

    /// <summary>
    /// Adds Endatix security middleware to the application.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatixSecurity(this IApplicationBuilder app)
    {
        // Create middleware builder and use security
        var builder = new EndatixMiddlewareBuilder(app);
        builder.UseSecurity();

        return app;
    }

    /// <summary>
    /// Adds Endatix exception handling middleware to the application.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatixExceptionHandler(this IApplicationBuilder app)
    {
        // Create middleware builder and use exception handler
        var builder = new EndatixMiddlewareBuilder(app);
        builder.UseExceptionHandler();

        return app;
    }

    /// <summary>
    /// Applies database migrations.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task ApplyDbMigrationsAsync(this IServiceProvider serviceProvider)
    {
        // Apply database migrations
        // Implementation depends on database provider and entity framework context

        return Task.CompletedTask;
    }

    /// <summary>
    /// Seeds the initial user.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task SeedInitialUserAsync(this WebApplication app)
    {
        // Seed initial user
        // Implementation depends on identity provider

        return Task.CompletedTask;
    }
}

/// <summary>
/// Options for configuring Endatix middleware.
/// </summary>
public class EndatixMiddlewareOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to use exception handling middleware.
    /// </summary>
    public bool UseExceptionHandler { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use API middleware.
    /// </summary>
    public bool UseApi { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use security middleware.
    /// </summary>
    public bool UseSecurity { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use HSTS middleware.
    /// </summary>
    public bool UseHsts { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use HTTPS redirection middleware.
    /// </summary>
    public bool UseHttpsRedirection { get; set; } = true;

    /// <summary>
    /// Gets or sets a delegate to configure API middleware options.
    /// </summary>
    public Action<EndatixApiMiddlewareOptions>? ApiOptions { get; set; }

    /// <summary>
    /// Gets or sets a delegate to configure additional middleware.
    /// </summary>
    public Action<IApplicationBuilder>? ConfigureAdditionalMiddleware { get; set; }
}

/// <summary>
/// Options for configuring Endatix API middleware.
/// </summary>
public class EndatixApiMiddlewareOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to use exception handling middleware.
    /// </summary>
    public bool UseExceptionHandler { get; set; } = true;

    /// <summary>
    /// Gets or sets the exception handler path.
    /// </summary>
    public string ExceptionHandlerPath { get; set; } = "/error";

    /// <summary>
    /// Gets or sets a value indicating whether to use Swagger middleware.
    /// </summary>
    public bool UseSwagger { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable Swagger in production.
    /// </summary>
    public bool EnableSwaggerInProduction { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to use CORS middleware.
    /// </summary>
    public bool UseCors { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use API versioning.
    /// </summary>
    public bool UseVersioning { get; set; } = true;

    /// <summary>
    /// Gets or sets the versioning prefix.
    /// </summary>
    public string VersioningPrefix { get; set; } = "v";

    /// <summary>
    /// Gets or sets the route prefix.
    /// </summary>
    public string RoutePrefix { get; set; } = "api";

    /// <summary>
    /// Gets or sets a delegate to configure FastEndpoints.
    /// </summary>
    public Action<FastEndpoints.Config>? ConfigureFastEndpoints { get; set; }
}