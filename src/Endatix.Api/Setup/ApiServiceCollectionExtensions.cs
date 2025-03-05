using Endatix.Api.Infrastructure.Cors;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Endatix.Api.Builders;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

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
    /// Adds API endpoints with default settings.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApiEndpoints(this IServiceCollection services, ILoggerFactory? loggerFactory = null)
    {
        var builder = new ApiConfigurationBuilder(services, loggerFactory);
        builder.UseDefaults();
        return services;
    }

    /// <summary>
    /// Adds API endpoints with default settings.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApiEndpoints(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILoggerFactory? loggerFactory = null)
    {
        var builder = new ApiConfigurationBuilder(services, configuration, environment, loggerFactory);
        builder.UseDefaults();
        return services;
    }

    /// <summary>
    /// Adds API endpoints with Swagger documentation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApiEndpointsWithSwagger(this IServiceCollection services, ILoggerFactory? loggerFactory = null)
    {
        var builder = new ApiConfigurationBuilder(services, loggerFactory);
        builder.UseDefaults()
               .AddSwagger();
        return services;
    }

    /// <summary>
    /// Adds API endpoints with Swagger documentation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApiEndpointsWithSwagger(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILoggerFactory? loggerFactory = null)
    {
        var builder = new ApiConfigurationBuilder(services, configuration, environment, loggerFactory);
        builder.UseDefaults()
               .AddSwagger();
        return services;
    }

    /// <summary>
    /// Adds API endpoints with versioning support.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApiEndpointsWithVersioning(this IServiceCollection services, ILoggerFactory? loggerFactory = null)
    {
        var builder = new ApiConfigurationBuilder(services, loggerFactory);
        builder.UseDefaults()
               .AddVersioning();
        return services;
    }

    /// <summary>
    /// Adds API endpoints with versioning support.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApiEndpointsWithVersioning(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILoggerFactory? loggerFactory = null)
    {
        var builder = new ApiConfigurationBuilder(services, configuration, environment, loggerFactory);
        builder.UseDefaults()
               .AddVersioning();
        return services;
    }

    /// <summary>
    /// Adds API endpoints with Swagger documentation and versioning support.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApiEndpointsWithSwaggerAndVersioning(this IServiceCollection services, ILoggerFactory? loggerFactory = null)
    {
        var builder = new ApiConfigurationBuilder(services, loggerFactory);
        builder.UseDefaults()
               .AddSwagger()
               .AddVersioning();
        return services;
    }

    /// <summary>
    /// Adds API endpoints with Swagger documentation and versioning support.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApiEndpointsWithSwaggerAndVersioning(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILoggerFactory? loggerFactory = null)
    {
        var builder = new ApiConfigurationBuilder(services, configuration, environment, loggerFactory);
        builder.UseDefaults()
               .AddSwagger()
               .AddVersioning();
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
        return new ApiConfigurationBuilder(services, loggerFactory);
    }

    /// <summary>
    /// Gets an API configuration builder for further configuration with configuration and environment.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>An API configuration builder.</returns>
    public static ApiConfigurationBuilder GetApiBuilder(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILoggerFactory? loggerFactory = null)
    {
        return new ApiConfigurationBuilder(services, configuration, environment, loggerFactory);
    }
}
