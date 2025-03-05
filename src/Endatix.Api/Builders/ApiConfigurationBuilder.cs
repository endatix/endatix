using FastEndpoints;
using FastEndpoints.Security;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NJsonSchema;
using System.Reflection;
using System.Text;
using Endatix.Api.Infrastructure;
using Endatix.Api.Infrastructure.Cors;
using Endatix.Infrastructure.Identity;
using Ardalis.GuardClauses;
using Microsoft.Extensions.FileProviders;
using Endatix.Framework.Setup;

namespace Endatix.Api.Builders;

/// <summary>
/// Builder for configuring API endpoints and related features.
/// </summary>
public class ApiConfigurationBuilder
{
    private readonly IServiceCollection _services;
    private readonly ILogger? _logger;
    private readonly IConfiguration? _configuration;
    private readonly IHostEnvironment? _environment;

    private const int JWT_CLOCK_SKEW_IN_SECONDS = 15;

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services => _services;

    /// <summary>
    /// Initializes a new instance of the ApiConfigurationBuilder class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    public ApiConfigurationBuilder(IServiceCollection services, ILoggerFactory? loggerFactory = null)
    {
        _services = services;
        _logger = loggerFactory?.CreateLogger("Endatix.Setup");
    }

    /// <summary>
    /// Initializes a new instance of the ApiConfigurationBuilder class with configuration and environment.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    public ApiConfigurationBuilder(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILoggerFactory? loggerFactory = null)
    {
        _services = services;
        _configuration = configuration;
        _environment = environment;
        _logger = loggerFactory?.CreateLogger("Endatix.Setup");
    }

    /// <summary>
    /// Configures API endpoints with default settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public virtual ApiConfigurationBuilder UseDefaults()
    {
        LogSetupInfo("Configuring API endpoints with default settings");

        // Add CORS services
        AddCorsServices();

        // Add default JSON options
        AddDefaultJsonOptions();

        // Add JWT authentication if configuration is available
        if (_configuration != null && _environment != null)
        {
            AddJwtAuthentication();
        }

        // Register FastEndpoints
        _services.AddFastEndpoints();

        // Add Swagger documentation
        AddSwagger();

        LogSetupInfo("API endpoints configured successfully");
        return this;
    }

    /// <summary>
    /// Adds JWT authentication with default settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public virtual ApiConfigurationBuilder AddJwtAuthentication()
    {
        Guard.Against.Null(_configuration, nameof(_configuration), "Configuration is required for JWT authentication");
        Guard.Against.Null(_environment, nameof(_environment), "Environment is required for JWT authentication");

        LogSetupInfo("Configuring JWT authentication");

        var jwtSettings = _configuration.GetRequiredSection(JwtOptions.SECTION_NAME).Get<JwtOptions>();
        Guard.Against.Null(jwtSettings, nameof(jwtSettings), "JWT settings are required for authentication");

        var isDevelopment = _environment.IsDevelopment();
        _services.AddAuthenticationJwtBearer(
            signingOptions => signingOptions.SigningKey = jwtSettings.SigningKey,
            bearerOptions =>
            {
                bearerOptions.RequireHttpsMetadata = !isDevelopment;
                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudiences = jwtSettings.Audiences,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(JWT_CLOCK_SKEW_IN_SECONDS)
                };
            });

        _services.AddAuthorization();

        LogSetupInfo("JWT authentication configured successfully");
        return this;
    }

    /// <summary>
    /// Adds CORS services with default settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public virtual ApiConfigurationBuilder AddCorsServices()
    {
        LogSetupInfo("Configuring CORS services");

        // Add CORS configuration services
        _services.AddTransient<EndpointsCorsConfigurator>();
        _services.AddTransient<IConfigureOptions<CorsOptions>, EndpointsCorsConfigurator>();
        _services.AddTransient<IWildcardSearcher, CorsWildcardSearcher>();

        _services.AddCors();
        _services.AddOptions<CorsSettings>()
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

        _services.Configure<JsonOptions>(options =>
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

        _services.SwaggerDocument(options =>
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

        _services.AddFastEndpoints(options =>
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
        if (_services.Any(sd => sd.ServiceType.Name.Contains("Swagger")))
        {
            app.UseSwaggerGen();
        }

        LogSetupInfo("API application configured successfully");
        return app;
    }

    /// <summary>
    /// Customizes the JWT authentication settings.
    /// </summary>
    /// <param name="configureJwt">The action to customize JWT authentication settings.</param>
    /// <returns>The builder for chaining.</returns>
    public virtual ApiConfigurationBuilder WithJwtAuthentication(Action<TokenValidationParameters> configureJwt)
    {
        Guard.Against.Null(_configuration, nameof(_configuration), "Configuration is required for JWT authentication");

        LogSetupInfo("Customizing JWT authentication");

        var jwtSettings = _configuration.GetRequiredSection(JwtOptions.SECTION_NAME).Get<JwtOptions>();
        Guard.Against.Null(jwtSettings, nameof(jwtSettings), "JWT settings are required for authentication");

        _services.AddAuthenticationJwtBearer(
            signingOptions => signingOptions.SigningKey = jwtSettings.SigningKey,
            bearerOptions =>
            {
                var tokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudiences = jwtSettings.Audiences,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(JWT_CLOCK_SKEW_IN_SECONDS)
                };

                // Apply custom configuration
                configureJwt(tokenValidationParameters);

                bearerOptions.TokenValidationParameters = tokenValidationParameters;
            });

        _services.AddAuthorization();

        LogSetupInfo("JWT authentication customized successfully");
        return this;
    }

    /// <summary>
    /// Customizes the CORS settings.
    /// </summary>
    /// <param name="configureCors">The action to customize CORS settings.</param>
    /// <returns>The builder for chaining.</returns>
    public virtual ApiConfigurationBuilder WithCorsServices(Action<CorsOptions> configureCors)
    {
        LogSetupInfo("Customizing CORS services");

        _services.AddCors(configureCors);
        _services.AddTransient<IWildcardSearcher, CorsWildcardSearcher>();

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

        _services.Configure<JsonOptions>(options =>
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
        _logger?.LogInformation("[API Setup] {Message}", message);
    }
}