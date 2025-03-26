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
    /// Adds API configuration options from appsettings.json
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>Updated service collection with API options configuration</returns>
    public static IServiceCollection AddApiOptions(this IServiceCollection services)
    {
        services.AddOptions<ApiOptions>()
                .BindConfiguration(ApiOptions.SECTION_NAME)
                .ValidateDataAnnotations();

        return services;
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
    private class AppEnvironmentAdapter : IAppEnvironment
    {
        private readonly IHostEnvironment _environment;

        public AppEnvironmentAdapter(IHostEnvironment environment)
        {
            _environment = environment;
        }

        public string EnvironmentName => _environment.EnvironmentName;

        public bool IsDevelopment() => _environment.EnvironmentName == "Development";
    }
}
