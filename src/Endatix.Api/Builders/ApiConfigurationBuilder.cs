using System.Reflection;
using Endatix.Api.Infrastructure;
using Endatix.Api.Infrastructure.Cors;
using Endatix.Api.Setup;
using Endatix.Framework.Hosting;
using Endatix.Framework.Serialization;
using Endatix.Framework.Setup;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NJsonSchema;

namespace Endatix.Api.Builders;

/// <summary>
/// Builder for configuring API endpoints and related features.
/// </summary>
public class ApiConfigurationBuilder
{
    private readonly ILogger? _logger;
    private readonly IConfiguration? _configuration;
    private readonly IAppEnvironment? _environment;

    private const int JWT_CLOCK_SKEW_IN_SECONDS = 15;

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Initializes a new instance of the ApiConfigurationBuilder class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration (optional).</param>
    /// <param name="environment">The application environment (optional).</param>
    /// <param name="loggerFactory">The logger factory (optional).</param>
    public ApiConfigurationBuilder(
        IServiceCollection services,
        IConfiguration? configuration = null,
        IAppEnvironment? environment = null,
        ILoggerFactory? loggerFactory = null)
    {
        Services = services;
        _configuration = configuration;
        _environment = environment;

        if (loggerFactory != null)
        {
            _logger = loggerFactory.CreateLogger<ApiConfigurationBuilder>();
        }
    }

    /// <summary>
    /// Configures API endpoints with default settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public virtual ApiConfigurationBuilder UseDefaults()
    {
        LogSetupInfo("Configuring API endpoints with default settings");

        if (_configuration != null)
        {
            Services.AddApiOptions(_configuration);
        }
        else
        {
            LogSetupInfo("No configuration provided. Using default settings.");
        }

        // Add CORS services
        AddCorsServices();

        // Add default JSON options
        AddDefaultJsonOptions();

        // Register FastEndpoints
        Services.AddFastEndpoints();

        // Add Swagger documentation
        AddSwagger();

        LogSetupInfo("API endpoints configured successfully");
        return this;
    }

    /// <summary>
    /// Adds CORS services to the service collection.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public virtual ApiConfigurationBuilder AddCorsServices()
    {
        LogSetupInfo("Configuring CORS services");

        // Ensure IAppEnvironment is available 
        if (Services.All(sd => sd.ServiceType != typeof(IAppEnvironment)))
        {
            LogSetupInfo("No IAppEnvironment found. Adding framework services...");

            // Use the framework's method to register IAppEnvironment and related services
            Services.AddEndatixFrameworkServices();
            LogSetupInfo("Registered IAppEnvironment via framework services.");
        }

        // Add CORS configuration services
        Services.AddTransient<EndpointsCorsConfigurator>();
        Services.AddTransient<IConfigureOptions<CorsOptions>, EndpointsCorsConfigurator>();
        Services.AddTransient<IWildcardSearcher, CorsWildcardSearcher>();

        Services.AddCors();
        Services.AddOptions<CorsSettings>()
               .BindConfiguration(CorsSettings.SECTION_NAME)
               .ValidateDataAnnotations();

        LogSetupInfo("CORS services configured successfully");
        return this;
    }

    /// <summary>
    /// Adds default JSON options for the API.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public virtual ApiConfigurationBuilder AddDefaultJsonOptions()
    {
        LogSetupInfo("Configuring default JSON options");

        Services.Configure<JsonOptions>(options =>
            options.SerializerOptions.Converters.Add(new LongToStringConverter()));

        LogSetupInfo("Default JSON options configured successfully");
        return this;
    }

    /// <summary>
    /// Adds Swagger documentation.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public virtual ApiConfigurationBuilder AddSwagger()
    {
        LogSetupInfo("Configuring Swagger documentation");

        Services.SwaggerDocument(options =>
        {
            options.ShortSchemaNames = true;
            options.DocumentSettings = settings =>
            {
                settings.Version = GetFormattedVersion();
                settings.Title = "Endatix Platform REST API";
                settings.DocumentName = "alpha-version";
                settings.Description = "The Endatix Platform is an open-source .NET library for data collection and management. This product is actively developed, and some API design characteristics may evolve. For more information, visit <a href=\"https://docs.endatix.com\">Endatix Documentation</a>.";
                settings.SchemaSettings.SchemaType = SchemaType.OpenApi3;
            };
            options.EndpointFilter = ep => {
                var hasHiddenTag = ep.EndpointTags?.Contains("hidden") ?? false;
                return hasHiddenTag ? false : true;
            };
        });

        LogSetupInfo("Swagger documentation configured successfully");
        return this;
    }

    /// <summary>
    /// Formats the Endatix API version string based on the provided Version in the Assembly.
    /// If the version is null, it returns a default version string "unknown".
    /// Otherwise, it formats the version string as "Major.Minor.Build-alpha|beta|rc".
    /// </summary>
    /// <returns>A formatted version string.</returns>
    private static string GetFormattedVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyVersion = assembly.GetName().Version;
        if (assemblyVersion is null)
        {
            return "unknown";
        }

        return $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
    }

    /// <summary>
    /// Configures API versioning.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public virtual ApiConfigurationBuilder AddVersioning()
    {
        LogSetupInfo("Configuring API versioning");

        // Simply log that versioning is enabled, but actual implementation 
        // needs to be added by the user or through a different configuration
        LogSetupInfo("API versioning support enabled - add specific versioning packages for full functionality");

        return this;
    }

    /// <summary>
    /// Adds endpoint discovery from the specified assemblies.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for endpoints.</param>
    /// <returns>The builder for chaining.</returns>
    public virtual ApiConfigurationBuilder ScanAssemblies(params Assembly[] assemblies)
    {
        LogSetupInfo($"Scanning {assemblies.Length} assemblies for endpoints");

        Services.AddFastEndpoints(options =>
        {
            options.Assemblies = assemblies;
        });

        LogSetupInfo("Assembly scanning for endpoints completed");
        return this;
    }

    /// <summary>
    /// Configures the API endpoint application builder with the registered services.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public virtual IApplicationBuilder ConfigureApplication(IApplicationBuilder app)
    {
        LogSetupInfo("Configuring API application");

        // Use FastEndpoints middleware
        app.UseFastEndpoints();

        // Check if Swagger is registered by looking for Swagger services
        if (Services.Any(sd => sd.ServiceType.Name.Contains("Swagger")))
        {
            app.UseSwaggerGen();
        }

        LogSetupInfo("API application configured successfully");
        return app;
    }

    /// <summary>
    /// Configures CORS services with custom options.
    /// </summary>
    /// <param name="configureCors">The action to configure CORS options.</param>
    /// <returns>The builder for chaining.</returns>
    public virtual ApiConfigurationBuilder WithCorsServices(Action<CorsOptions> configureCors)
    {
        LogSetupInfo("Customizing CORS services");

        Services.AddCors(configureCors);
        Services.AddTransient<IWildcardSearcher, CorsWildcardSearcher>();

        LogSetupInfo("CORS services customized successfully");
        return this;
    }

    /// <summary>
    /// Customizes the JSON options.
    /// </summary>
    /// <param name="configureJson">The action to customize JSON options.</param>
    /// <returns>The builder for chaining.</returns>
    public virtual ApiConfigurationBuilder WithJsonOptions(Action<JsonOptions> configureJson)
    {
        LogSetupInfo("Customizing JSON options");

        Services.Configure<JsonOptions>(options =>
        {
            // Add default LongToStringConverter
            options.SerializerOptions.Converters.Add(new LongToStringConverter());

            // Apply custom configuration
            configureJson(options);
        });

        LogSetupInfo("JSON options customized successfully");
        return this;
    }

    protected virtual void LogSetupInfo(string message)
    {
        _logger?.LogDebug(message);
    }
}