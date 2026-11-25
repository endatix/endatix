using Endatix.Api.Infrastructure.Cors;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Endatix.Api.Builders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Endatix.Framework.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Endatix.Api.Configuration;
using Endatix.Framework.Configuration;

namespace Endatix.Api.Setup;

/// <summary>
/// Extension methods for configuring API endpoints with IServiceCollection.
/// </summary>
public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Adds the CORS middleware required to run the <see cref="Endatix.Api" /> project
    /// </summary>
    /// <param name="services">the <see cref="IServiceCollection"/> services</param>
    /// <returns>Updated <see cref="IServiceCollection"/> with CORS related middleware and services</returns>
    public static IServiceCollection AddCorsServices(this IServiceCollection services)
    {
        services.AddCors();
        services.AddOptions<CorsSettings>()
               .BindConfiguration(CorsSettings.SECTION_NAME)
               .ValidateDataAnnotations();

        services.AddTransient<IConfigureOptions<CorsOptions>, EndpointsCorsConfigurator>();
        services.AddTransient<IWildcardSearcher, CorsWildcardSearcher>();

        return services;
    }

    /// <summary>
    /// Adds API configuration options from appsettings.json and maps them to ApiOptions used throughout the application.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The application configuration</param>
    /// <returns>Updated service collection with API options configuration</returns>
    public static IServiceCollection AddApiOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEndatixOptions<ApiConfigurationOptions>(configuration);

        services.AddTransient<IConfigureOptions<ApiOptions>>(provider =>
        {
            var config = provider.GetRequiredService<IOptions<ApiConfigurationOptions>>().Value;
            var legacy = LegacyDocsKeys.Read(configuration);
            return new ConfigureNamedOptions<ApiOptions>(
                Options.DefaultName, options => MapApiOptions(config, legacy, options));
        });

        return services;
    }

    /// <summary>
    /// Maps the docs settings: each nested <c>Docs</c> key wins over its legacy key, and a pair set to different values
    /// is recorded, so the API docs warn about it at startup.
    /// </summary>
    private static void MapApiOptions(ApiConfigurationOptions config, LegacyDocsKeys legacy, ApiOptions options)
    {
        var docs = config.Docs;
        options.Docs.Enabled = docs.Enabled ?? config.UseSwagger;
        if (docs.Path is not null)
        {
            options.Docs.Path = docs.Path;
        }
        else if (legacy.SwaggerPath is not null)
        {
            options.SwaggerPath = legacy.SwaggerPath;
        }

        options.Docs.SwaggerUi.Enabled = docs.SwaggerUi.Enabled;
        options.Docs.SwaggerUi.Path = docs.SwaggerUi.Path;
        options.Docs.Conflicts = DocsConflicts(config, legacy);
    }

    private static List<ApiDocsSettingConflict> DocsConflicts(ApiConfigurationOptions config, LegacyDocsKeys legacy)
    {
        List<ApiDocsSettingConflict> conflicts = [];
        if (legacy.UseSwaggerSet && config.Docs.Enabled is { } enabled && enabled != config.UseSwagger)
        {
            conflicts.Add(new("Endatix:Api:Docs:Enabled", enabled.ToString(), "Endatix:Api:UseSwagger", config.UseSwagger.ToString()));
        }

        if (config.Docs.Path is { } path && legacy.SwaggerPath is { } swaggerPath
            && !string.Equals(path, swaggerPath, StringComparison.Ordinal))
        {
            conflicts.Add(new(ApiDocsOptions.PATH_SETTING, path, ApiDocsOptions.SWAGGER_PATH_SETTING, swaggerPath));
        }

        return conflicts;
    }

    /// <summary>
    /// Adds API endpoints with default settings.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApiEndpoints(this IServiceCollection services, ILoggerFactory? loggerFactory = null)
    {
        var builder = new ApiConfigurationBuilder(services, loggerFactory: loggerFactory);
        builder.UseDefaults();
        return services;
    }

    /// <summary>
    /// Adds API endpoints with configuration and environment.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The application environment.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddApiEndpoints(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILoggerFactory? loggerFactory = null)
    {
        // Register the environment as a singleton
        services.TryAddSingleton(environment);

        // Get our builder and configure it
        var appBuilder = services.GetApiBuilder(configuration, environment, loggerFactory);
        appBuilder.AddCorsServices()
                 .UseDefaults();

        return services;
    }

    /// <summary>
    /// Adds API endpoints with Swagger documentation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddApiEndpointsWithSwagger(this IServiceCollection services, ILoggerFactory? loggerFactory = null)
    {
        services.GetApiBuilder(loggerFactory)
            .AddCorsServices()
            .AddSwagger()
            .UseDefaults();

        return services;
    }

    /// <summary>
    /// Adds API endpoints with Swagger documentation and configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The application environment.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddApiEndpointsWithSwagger(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILoggerFactory? loggerFactory = null)
    {
        // Register the environment as a singleton
        services.TryAddSingleton(environment);

        // Get our builder and configure it
        var appBuilder = services.GetApiBuilder(configuration, environment, loggerFactory);
        appBuilder.AddCorsServices()
                 .AddSwagger()
                 .UseDefaults();

        return services;
    }

    /// <summary>
    /// Adds API endpoints with versioning support.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddApiEndpointsWithVersioning(this IServiceCollection services, ILoggerFactory? loggerFactory = null)
    {
        services.GetApiBuilder(loggerFactory)
            .AddCorsServices()
            .AddVersioning()
            .UseDefaults();

        return services;
    }

    /// <summary>
    /// Adds API endpoints with versioning support and configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The application environment.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddApiEndpointsWithVersioning(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILoggerFactory? loggerFactory = null)
    {
        // Register the environment as a singleton
        services.TryAddSingleton(environment);

        // Get our builder and configure it
        var appBuilder = services.GetApiBuilder(configuration, environment, loggerFactory);
        appBuilder.AddCorsServices()
                 .AddVersioning()
                 .UseDefaults();

        return services;
    }

    /// <summary>
    /// Adds API endpoints with Swagger documentation and versioning support.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddApiEndpointsWithSwaggerAndVersioning(this IServiceCollection services, ILoggerFactory? loggerFactory = null)
    {
        services.GetApiBuilder(loggerFactory)
            .AddCorsServices()
            .AddSwagger()
            .AddVersioning()
            .UseDefaults();

        return services;
    }

    /// <summary>
    /// Adds API endpoints with Swagger documentation, versioning support, and configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The application environment.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddApiEndpointsWithSwaggerAndVersioning(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILoggerFactory? loggerFactory = null)
    {
        // Register the environment as a singleton
        services.TryAddSingleton(environment);

        // Get our builder and configure it
        var appBuilder = services.GetApiBuilder(configuration, environment, loggerFactory);
        appBuilder.AddCorsServices()
                 .AddSwagger()
                 .AddVersioning()
                 .UseDefaults();

        return services;
    }

    /// <summary>
    /// Gets an API configuration builder for further configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>An API configuration builder.</returns>
    public static ApiConfigurationBuilder GetApiBuilder(this IServiceCollection services, ILoggerFactory? loggerFactory = null)
    {
        return new ApiConfigurationBuilder(services, loggerFactory: loggerFactory);
    }

    /// <summary>
    /// Gets an API configuration builder for further configuration with configuration and environment.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>An API configuration builder.</returns>
    public static ApiConfigurationBuilder GetApiBuilder(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILoggerFactory? loggerFactory = null)
    {
        // Register the environment
        services.TryAddSingleton(environment);

        // Create the adapter for IAppEnvironment
        var appEnvironment = new AppEnvironmentAdapter(environment);

        // Pass it to the ApiConfigurationBuilder
        return new ApiConfigurationBuilder(services, configuration, appEnvironment, loggerFactory);
    }

    // Helper class for adapting IHostEnvironment to IAppEnvironment
    private sealed class AppEnvironmentAdapter : IAppEnvironment
    {
        private readonly IHostEnvironment _environment;

        public AppEnvironmentAdapter(IHostEnvironment environment)
        {
            _environment = environment;
        }

        public string EnvironmentName => _environment.EnvironmentName;

        public bool IsDevelopment() => _environment.EnvironmentName == "Development";
    }

    /// <summary>
    /// Which legacy docs keys the configuration holds, and the raw <c>SwaggerPath</c>. Their properties keep their
    /// shipped defaults (<c>true</c> and <c>"/api-docs"</c>), so only the raw keys tell a set value from the default.
    /// An empty value counts as set, so <c>SwaggerPath: ""</c> still asks for the root.
    /// </summary>
    private sealed record LegacyDocsKeys(bool UseSwaggerSet, string? SwaggerPath)
    {
        public static LegacyDocsKeys Read(IConfiguration configuration)
        {
            var section = configuration.GetSection(EndatixOptionsBase.GetSectionName<ApiConfigurationOptions>());
            return new(
                section[nameof(ApiConfigurationOptions.UseSwagger)] is not null,
                section[nameof(ApiConfigurationOptions.SwaggerPath)]);
        }
    }
}
