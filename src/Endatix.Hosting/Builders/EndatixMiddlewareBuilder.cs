using System.Security.Claims;
using Endatix.Api.Builders;
using Endatix.Api.Infrastructure;
using Endatix.Api.Setup;
using Endatix.Framework.Serialization;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Multitenancy;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSwag.AspNetCore;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Builder for configuring middleware in the Endatix application.
/// </summary>
public class EndatixMiddlewareBuilder
{
    private readonly ILogger? _logger;

    /// <summary>
    /// Gets the application builder.
    /// </summary>
    public IApplicationBuilder App { get; }

    /// <summary>
    /// Initializes a new instance of the EndatixMiddlewareBuilder class.
    /// </summary>
    /// <param name="app">The application builder.</param>
    public EndatixMiddlewareBuilder(IApplicationBuilder app)
    {
        App = app;
        _logger = app.ApplicationServices.GetService<ILoggerFactory>()?.CreateLogger("Endatix.Middleware");
    }

    /// <summary>
    /// Configures the middleware with default settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseDefaults()
    {
        _logger?.LogInformation("Configuring middleware with default settings");

        UseExceptionHandler()
            .UseSecurity()
            .UseMultitenancy()
            .UseHsts()
            .UseHttpsRedirection()
            .UseApi()
            .UseHealthChecks();

        _logger?.LogInformation("Middleware configured with default settings");
        return this;
    }

    /// <summary>
    /// Adds exception handling middleware.
    /// </summary>
    /// <param name="path">The path for exception handling.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseExceptionHandler(string path = "/error")
    {
        _logger?.LogInformation($"Adding exception handler middleware with path: {path}");
        App.UseExceptionHandler(path);
        return this;
    }

    /// <summary>
    /// Adds security middleware (authentication and authorization).
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseSecurity()
    {
        _logger?.LogInformation("Adding security middleware");
        App.UseAuthentication();
        App.UseAuthorization();
        return this;
    }

    /// <summary>
    /// Adds multitenancy middleware.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseMultitenancy()
    {
        _logger?.LogInformation("Adding multitenancy middleware");
        App.UseMiddleware<TenantMiddleware>();
        return this;
    }

    /// <summary>
    /// Adds HSTS middleware.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseHsts()
    {
        _logger?.LogInformation("Adding HSTS middleware");
        App.UseHsts();
        return this;
    }

    /// <summary>
    /// Adds HTTPS redirection middleware.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseHttpsRedirection()
    {
        _logger?.LogInformation("Adding HTTPS redirection middleware");
        App.UseHttpsRedirection();
        return this;
    }

    /// <summary>
    /// Adds API middleware with optional custom settings.
    /// </summary>
    /// <param name="configureApi">Optional action to configure API options.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseApi(Action<ApiOptions>? configureApi = null)
    {
        _logger?.LogInformation(configureApi == null
            ? "Adding API middleware with default settings"
            : "Adding API middleware with custom settings");

        var optionsProvider = App.ApplicationServices.GetService<IOptions<ApiOptions>>();
        var options = optionsProvider?.Value ?? new ApiOptions();
        
        _logger?.LogInformation("Loaded ApiOptions from configuration: UseSwagger={UseSwagger}, SwaggerPath={SwaggerPath}",
            options.UseSwagger,
            options.SwaggerPath);
        
        // Apply any additional configuration if provided
        configureApi?.Invoke(options);

        UseFastEndpoints(config =>
        {
            config.Versioning.Prefix = options.VersioningPrefix;
            config.Endpoints.RoutePrefix = options.RoutePrefix;
            config.Serializer.Options.Converters.Add(new LongToStringConverter());
            config.Security.RoleClaimType = ClaimTypes.Role;
            config.Security.PermissionsClaimType = ClaimNames.Permission;

            // Apply any custom FastEndpoints configuration
            options.ConfigureFastEndpoints?.Invoke(config);
        });

        // Apply Swagger middleware if enabled through configuration
        if (options.UseSwagger)
        {
            _logger?.LogInformation("Swagger UI enabled with path: {SwaggerPath}", options.SwaggerPath);
            UseSwagger(
                options.SwaggerPath,
                options.ConfigureOpenApiDocument,
                options.ConfigureSwaggerUi);
        }
        else
        {
            _logger?.LogInformation("Swagger UI disabled through configuration");
        }

        if (options.UseCors)
        {
            App.UseCors();
        }

        return this;
    }

    /// <summary>
    /// Configures FastEndpoints with the specified configuration.
    /// </summary>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseFastEndpoints(Action<FastEndpoints.Config> configure)
    {
        _logger?.LogInformation("Configuring FastEndpoints middleware");
        App.UseFastEndpoints(configure);
        return this;
    }

    /// <summary>
    /// Adds Swagger middleware with optional custom settings.
    /// </summary>
    /// <param name="path">Optional custom path for Swagger UI endpoint. Defaults to "/swagger".</param>
    /// <param name="configureOpenApi">Optional delegate to configure OpenAPI document generation settings.</param>
    /// <param name="configureSwaggerUi">Optional delegate to configure Swagger UI display settings.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseSwagger(string? path = null, Action<OpenApiDocumentMiddlewareSettings>? configureOpenApi = null, Action<SwaggerUiSettings>? configureSwaggerUi = null)
    {
        _logger?.LogInformation(path == null
            ? "Adding Swagger middleware with default settings"
            : $"Adding Swagger middleware with custom path: {path}");

        App.UseSwaggerGen(configureOpenApi, uiConfig =>
        {
            if (path != null)
            {
                uiConfig.Path = path;
            }
            configureSwaggerUi?.Invoke(uiConfig);
        });

        return this;
    }

    /// <summary>
    /// Adds API endpoints using the ApiApplicationBuilderExtensions.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseApiEndpoints()
    {
        _logger?.LogInformation("Adding API endpoints");
        App.UseApiEndpoints();
        return this;
    }

    /// <summary>
    /// Adds API endpoints with custom configuration.
    /// </summary>
    /// <param name="configureApi">The configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseApiEndpoints(Action<ApiOptions> configureApi)
    {
        _logger?.LogInformation("Adding API endpoints with custom configuration");
        App.UseApiEndpoints(configureApi);
        return this;
    }

    /// <summary>
    /// Adds health checks middleware with default settings.
    /// </summary>
    /// <param name="path">Optional custom path for health checks. Defaults to "/health".</param>
    /// <param name="configureHealthChecks">Optional delegate to configure health checks options.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseHealthChecks(string path = "/health", Action<EndatixHealthChecksMiddlewareBuilder>? configureHealthChecks = null)
    {
        _logger?.LogInformation($"Adding health checks middleware with path: {path}");

        // Use the dedicated health checks middleware builder to avoid code duplication
        var healthChecksBuilder = new EndatixHealthChecksMiddlewareBuilder(this, _logger);

        // Apply custom configuration if provided
        if (configureHealthChecks is { })
        {
            configureHealthChecks(healthChecksBuilder);
        }

        // Configure the health checks middleware
        healthChecksBuilder.WithPath(path);
        healthChecksBuilder.Apply(App);

        return this;
    }
}
