using Endatix.Api.Builders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Builder for configuring Endatix API.
/// </summary>
public class EndatixApiBuilder
{
    private readonly EndatixBuilder _parentBuilder;
    private readonly ILogger? _logger;
    private readonly ApiConfigurationBuilder _apiConfigurationBuilder;
    private readonly EndatixApiMiddlewareOptions _middlewareOptions;

    /// <summary>
    /// Initializes a new instance of the EndatixApiBuilder class.
    /// </summary>
    /// <param name="parentBuilder">The parent builder.</param>
    internal EndatixApiBuilder(EndatixBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
        _logger = parentBuilder.LoggerFactory?.CreateLogger<EndatixApiBuilder>();
        _middlewareOptions = new EndatixApiMiddlewareOptions();

        // Create the API configuration builder with all available parameters
        _apiConfigurationBuilder = new ApiConfigurationBuilder(
            parentBuilder.Services,
            parentBuilder.Configuration,
            parentBuilder.AppEnvironment,
            parentBuilder.LoggerFactory);

        // Log a warning if environment is missing
        if (parentBuilder.AppEnvironment is null)
        {
            _logger?.LogWarning("No application environment was provided to EndatixBuilder. Some features may not be fully available.");
        }
    }

    /// <summary>
    /// Configures API with default settings.
    /// </summary>
    /// <returns>The API builder for chaining.</returns>
    public EndatixApiBuilder UseDefaults()
    {
        LogSetupInfo("Configuring API with default settings");

        // Use the API configuration builder with defaults
        _apiConfigurationBuilder.UseDefaults();

        LogSetupInfo("API configuration completed");
        return this;
    }

    /// <summary>
    /// Adds Swagger documentation.
    /// </summary>
    /// <returns>The API builder for chaining.</returns>
    public EndatixApiBuilder AddSwagger()
    {
        LogSetupInfo("Adding Swagger documentation");

        _apiConfigurationBuilder.AddSwagger();
        _middlewareOptions.UseSwagger = true;

        LogSetupInfo("Swagger documentation added");
        return this;
    }

    /// <summary>
    /// Disables Swagger documentation.
    /// </summary>
    /// <returns>The API builder for chaining.</returns>
    public EndatixApiBuilder DisableSwagger()
    {
        LogSetupInfo("Disabling Swagger documentation");

        _middlewareOptions.UseSwagger = false;

        LogSetupInfo("Swagger documentation disabled");
        return this;
    }

    /// <summary>
    /// Adds API versioning.
    /// </summary>
    /// <returns>The API builder for chaining.</returns>
    public EndatixApiBuilder AddVersioning()
    {
        LogSetupInfo("Adding API versioning");

        _apiConfigurationBuilder.AddVersioning();

        LogSetupInfo("API versioning added");
        return this;
    }

    /// <summary>
    /// Sets the API versioning prefix.
    /// </summary>
    /// <param name="prefix">The versioning prefix.</param>
    /// <returns>The API builder for chaining.</returns>
    public EndatixApiBuilder SetVersioningPrefix(string prefix)
    {
        LogSetupInfo($"Setting API versioning prefix to '{prefix}'");

        _middlewareOptions.VersioningPrefix = prefix;

        LogSetupInfo($"API versioning prefix set to '{prefix}'");
        return this;
    }

    /// <summary>
    /// Sets the API route prefix.
    /// </summary>
    /// <param name="prefix">The route prefix.</param>
    /// <returns>The API builder for chaining.</returns>
    public EndatixApiBuilder SetRoutePrefix(string prefix)
    {
        LogSetupInfo($"Setting API route prefix to '{prefix}'");

        _middlewareOptions.RoutePrefix = prefix;

        LogSetupInfo($"API route prefix set to '{prefix}'");
        return this;
    }

    /// <summary>
    /// Enables CORS with the specified policy.
    /// </summary>
    /// <param name="policyName">The policy name.</param>
    /// <param name="configurePolicy">The policy configuration action.</param>
    /// <returns>The API builder for chaining.</returns>
    public EndatixApiBuilder EnableCors(string policyName, Action<CorsPolicyBuilder> configurePolicy)
    {
        LogSetupInfo($"Enabling CORS with policy '{policyName}'");

        _parentBuilder.Services.AddCors(options =>
        {
            options.AddPolicy(policyName, configurePolicy);
        });

        _middlewareOptions.UseCors = true;

        LogSetupInfo("CORS enabled");
        return this;
    }

    /// <summary>
    /// Disables CORS.
    /// </summary>
    /// <returns>The API builder for chaining.</returns>
    public EndatixApiBuilder DisableCors()
    {
        LogSetupInfo("Disabling CORS");

        _middlewareOptions.UseCors = false;

        LogSetupInfo("CORS disabled");
        return this;
    }

    /// <summary>
    /// Scans assemblies for API endpoints.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>The API builder for chaining.</returns>
    public EndatixApiBuilder ScanAssemblies(params Assembly[] assemblies)
    {
        LogSetupInfo($"Scanning {assemblies.Length} assemblies for API endpoints");

        _apiConfigurationBuilder.ScanAssemblies(assemblies);

        LogSetupInfo("Assembly scanning completed");
        return this;
    }

    /// <summary>
    /// Configures FastEndpoints using the specified action.
    /// </summary>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The API builder for chaining.</returns>
    public EndatixApiBuilder ConfigureFastEndpoints(Action<FastEndpoints.Config> configure)
    {
        LogSetupInfo("Configuring FastEndpoints");

        _middlewareOptions.ConfigureFastEndpoints = configure;

        LogSetupInfo("FastEndpoints configuration added");
        return this;
    }

    /// <summary>
    /// Configures the application middleware for the API.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder.</returns>
    public IApplicationBuilder ConfigureApplication(IApplicationBuilder app)
    {
        LogSetupInfo("Configuring API application middleware");

        // Use the Endatix API middleware with our custom options
        app.UseEndatixApi(options =>
        {
            // Copy all settings from our builder to the middleware options
            options.UseSwagger = _middlewareOptions.UseSwagger;
            options.UseCors = _middlewareOptions.UseCors;
            options.VersioningPrefix = _middlewareOptions.VersioningPrefix;
            options.RoutePrefix = _middlewareOptions.RoutePrefix;
            options.ConfigureFastEndpoints = _middlewareOptions.ConfigureFastEndpoints;
        });

        LogSetupInfo("API application middleware configured");
        return app;
    }

    /// <summary>
    /// Returns to the parent builder.
    /// </summary>
    /// <returns>The parent builder for chaining.</returns>
    public EndatixBuilder Build() => _parentBuilder;

    private void LogSetupInfo(string message)
    {
        _logger?.LogInformation("[API Setup] {Message}", message);
    }
}