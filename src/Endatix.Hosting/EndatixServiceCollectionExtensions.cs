using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Ardalis.GuardClauses;
using Endatix.Hosting.Builders;
using Endatix.Hosting.Core;
using Endatix.Hosting.Internal;

namespace Endatix.Hosting;

/// <summary>
/// Extension methods for configuring Endatix services.
/// </summary>
public static class EndatixServiceCollectionExtensions
{
    /// <summary>
    /// Adds Endatix services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>An EndatixBuilder for further configuration.</returns>
    public static EndatixBuilder AddEndatix(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        Guard.Against.Null(services);
        Guard.Against.Null(configuration);

        // Register core services
        services.AddEndatixFrameworkServices();

        // Register options from configuration
        services.Configure<Options.EndatixOptions>(
            configuration.GetSection("Endatix"));

        // Create and return the builder
        return new EndatixBuilder(services, configuration);
    }

    /// <summary>
    /// Adds Endatix services with default configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEndatixWithDefaults(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddEndatix(configuration)
            .UseDefaults();

        return services;
    }

    /// <summary>
    /// Adds the required Endatix framework services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    internal static IServiceCollection AddEndatixFrameworkServices(this IServiceCollection services)
    {
        // Register core framework services
        services.AddSingleton<IAppEnvironment, AppEnvironment>();

        return services;
    }

    /// <summary>
    /// Adds Endatix services with default configuration.
    /// </summary>
    public static WebApplication UseEndatix(
        this WebApplication app)
    {
        Guard.Against.Null(app);

        // Configure middleware in the correct order
        app.UseEndatixExceptionHandler()
           .UseEndatixSecurity()
           .UseEndatixApi();

        return app;
    }
}