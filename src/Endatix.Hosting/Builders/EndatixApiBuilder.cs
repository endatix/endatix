using System.Reflection;
using System.Security.Claims;
using Endatix.Api.Builders;
using Endatix.Api.Infrastructure;
using Endatix.Framework.Serialization;
using Endatix.Infrastructure.Identity;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Builder for configuring Endatix API features including versioning, Swagger, CORS, and endpoint routing.
/// </summary>
/// <remarks>
/// The EndatixApiBuilder provides a fluent API for configuring all API-related services and middleware.
/// This includes:
/// <list type="bullet">
/// <item><description>API documentation with Swagger</description></item>
/// <item><description>API versioning</description></item>
/// <item><description>CORS policies</description></item>
/// <item><description>Route prefixes</description></item>
/// <item><description>FastEndpoints configuration</description></item>
/// </list>
/// 
/// You typically obtain an instance of this builder through the <see cref="EndatixBuilder.Api"/> property.
/// </remarks>
public class EndatixApiBuilder
{
    private readonly EndatixBuilder _parentBuilder;
    private readonly ILogger? _logger;
    private readonly ApiConfigurationBuilder _apiConfigurationBuilder;
    private readonly ApiOptions _apiOptions;

    /// <summary>
    /// Initializes a new instance of the EndatixApiBuilder class.
    /// </summary>
    /// <param name="parentBuilder">The parent builder.</param>
    internal EndatixApiBuilder(EndatixBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
        _logger = parentBuilder.LoggerFactory?.CreateLogger<EndatixApiBuilder>();
        _apiOptions = new ApiOptions();

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
    /// <remarks>
    /// This method:
    /// <list type="bullet">
    /// <item><description>Enables Swagger documentation</description></item>
    /// <item><description>Enables API versioning</description></item>
    /// <item><description>Configures default CORS policies</description></item>
    /// <item><description>Configures FastEndpoints with recommended settings</description></item>
    /// </list>
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Configure API with defaults
    /// builder.Host.UseEndatix(endatix => endatix
    ///     .WithApi(api => api
    ///         .UseDefaults()));
    /// </code>
    /// </example>
    /// </remarks>
    /// <returns>The API builder for chaining.</returns>
    public EndatixApiBuilder UseDefaults()
    {
        LogSetupInfo("Configuring API with default settings");

        // Use the API configuration builder with defaults
        _apiConfigurationBuilder.UseDefaults();

        // Set default options
        _apiOptions.UseSwagger = true;
        _apiOptions.UseVersioning = true;
        _apiOptions.UseCors = true;

        LogSetupInfo("API configuration completed");
        return this;
    }

    /// <summary>
    /// Adds Swagger documentation.
    /// </summary>
    /// <remarks>
    /// This method enables Swagger documentation for your API, which provides:
    /// <list type="bullet">
    /// <item><description>Interactive API documentation</description></item>
    /// <item><description>Request/response models and examples</description></item>
    /// <item><description>Built-in client for testing API endpoints</description></item>
    /// </list>
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Add Endatix with Swagger
    /// builder.Services.AddEndatix(builder.Configuration)
    ///     .Api.AddSwagger();
    /// </code>
    /// </example>
    /// </remarks>
    /// <returns>The API builder for chaining.</returns>
    public EndatixApiBuilder AddSwagger()
    {
        LogSetupInfo("Adding Swagger documentation");

        _apiConfigurationBuilder.AddSwagger();
        _apiOptions.UseSwagger = true;

        LogSetupInfo("Swagger documentation added");
        return this;
    }

    /// <summary>
    /// Disables Swagger documentation.
    /// </summary>
    /// <remarks>
    /// Use this method to disable Swagger in production environments or when you don't want 
    /// to expose API documentation.
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Add Endatix with conditional Swagger
    /// var endatixBuilder = builder.Services.AddEndatix(builder.Configuration);
    /// 
    /// if (builder.Environment.IsDevelopment())
    /// {
    ///     endatixBuilder.Api.AddSwagger();
    /// }
    /// else
    /// {
    ///     endatixBuilder.Api.DisableSwagger();
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    /// <returns>The API builder for chaining.</returns>
    public EndatixApiBuilder DisableSwagger()
    {
        LogSetupInfo("Disabling Swagger documentation");

        _apiOptions.UseSwagger = false;

        LogSetupInfo("Swagger documentation disabled");
        return this;
    }

    /// <summary>
    /// Adds API versioning.
    /// </summary>
    /// <remarks>
    /// This method enables API versioning, which allows you to maintain multiple versions 
    /// of your API endpoints simultaneously. Versioning is implemented using URL path segments.
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Add Endatix with API versioning
    /// builder.Services.AddEndatix(builder.Configuration)
    ///     .Api.AddVersioning()
    ///     .SetVersioningPrefix("v"); // Results in URLs like /api/v1/resource
    /// </code>
    /// </example>
    /// </remarks>
    /// <returns>The API builder for chaining.</returns>
    public EndatixApiBuilder AddVersioning()
    {
        LogSetupInfo("Adding API versioning");

        _apiConfigurationBuilder.AddVersioning();
        _apiOptions.UseVersioning = true;

        LogSetupInfo("API versioning added");
        return this;
    }

    /// <summary>
    /// Sets the API versioning prefix.
    /// </summary>
    /// <remarks>
    /// This method allows you to customize how version numbers appear in URLs.
    /// The default prefix is "v", resulting in URLs like /api/v1/resource.
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Add Endatix with custom version prefix
    /// builder.Services.AddEndatix(builder.Configuration)
    ///     .Api.AddVersioning()
    ///     .SetVersioningPrefix("version"); // Results in URLs like /api/version1/resource
    /// </code>
    /// </example>
    /// </remarks>
    /// <param name="prefix">The versioning prefix.</param>
    /// <returns>The API builder for chaining.</returns>
    public EndatixApiBuilder SetVersioningPrefix(string prefix)
    {
        LogSetupInfo($"Setting API versioning prefix to '{prefix}'");

        _apiOptions.VersioningPrefix = prefix;

        LogSetupInfo($"API versioning prefix set to '{prefix}'");
        return this;
    }

    /// <summary>
    /// Sets the API route prefix.
    /// </summary>
    /// <remarks>
    /// This method allows you to customize the base path for all API endpoints.
    /// The default prefix is "api", resulting in URLs like /api/v1/resource.
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Add Endatix with custom route prefix
    /// builder.Services.AddEndatix(builder.Configuration)
    ///     .Api.SetRoutePrefix("services"); // Results in URLs like /services/v1/resource
    /// </code>
    /// </example>
    /// </remarks>
    /// <param name="prefix">The route prefix.</param>
    /// <returns>The API builder for chaining.</returns>
    public EndatixApiBuilder SetRoutePrefix(string prefix)
    {
        LogSetupInfo($"Setting API route prefix to '{prefix}'");

        _apiOptions.RoutePrefix = prefix;

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

        _apiOptions.UseCors = true;

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

        _apiOptions.UseCors = false;

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

        _apiOptions.ConfigureFastEndpoints = configure;

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

        // Apply exception handling middleware if enabled
        if (_apiOptions.UseExceptionHandler)
        {
            app.UseExceptionHandler(_apiOptions.ExceptionHandlerPath);
        }

        // Apply FastEndpoints middleware with configuration
        app.UseFastEndpoints(c =>
        {
            // Apply versioning configuration
            c.Versioning.Prefix = _apiOptions.VersioningPrefix;
            c.Endpoints.RoutePrefix = _apiOptions.RoutePrefix;

            // Apply serializer configuration
            c.Serializer.Options.Converters.Add(new LongToStringConverter());

            // Apply security configuration
            c.Security.RoleClaimType = ClaimTypes.Role;
            c.Security.PermissionsClaimType = ClaimNames.Permission;

            // Apply any custom configuration
            _apiOptions.ConfigureFastEndpoints?.Invoke(c);
        });

        // Apply Swagger middleware if enabled
        if (_apiOptions.UseSwagger)
        {
            LogSetupInfo($"Enabling Swagger UI with path: {_apiOptions.SwaggerPath}");
            app.UseSwaggerGen();
        }
        else
        {
            LogSetupInfo("Swagger UI disabled through configuration");
        }

        // Apply CORS middleware if enabled
        if (_apiOptions.UseCors)
        {
            app.UseCors();
        }

        LogSetupInfo("API application middleware configured");
        return app;
    }

    /// <summary>
    /// Builds and returns the parent builder.
    /// </summary>
    /// <returns>The parent builder.</returns>
    public EndatixBuilder Build() => _parentBuilder;

    private void LogSetupInfo(string message)
    {
        _logger?.LogDebug("[API Setup] {Message}", message);
    }
}