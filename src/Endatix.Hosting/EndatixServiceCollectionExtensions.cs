using Ardalis.GuardClauses;
using Endatix.Framework.Configuration;
using Endatix.Framework.Setup;
using Endatix.Hosting.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Hosting;

/// <summary>
/// Extension methods for configuring Endatix services.
/// </summary>
internal static class EndatixServiceCollectionExtensions
{
    /// <summary>
    /// Adds Endatix services to the service collection. This is the entry point for configuring Endatix.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>A builder for further configuration.</returns>
    internal static EndatixBuilder AddEndatix(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        Guard.Against.Null(services);
        Guard.Against.Null(configuration);

        // Register core framework services FIRST to ensure IAppEnvironment is available∏∏
        RegisterCoreFrameworkServices(services, configuration);

        // Register standard Endatix options
        services.AddStandardEndatixOptions(configuration);

        // Now create the logging builder which will get environment from services
        var loggingBuilder = new EndatixLoggingBuilder(services, configuration);
        var loggerFactory = loggingBuilder.GetComponents();

        // Create the main builder with logging already configured
        var builder = new EndatixBuilder(services, configuration);
        var logger = builder.LoggerFactory.CreateLogger(typeof(EndatixServiceCollectionExtensions));

        // Register remaining services
        logger.LogInformation("Registering identity services");
        RegisterIdentityServices(services, configuration);

        return builder;
    }

    private static void RegisterCoreFrameworkServices(IServiceCollection services, IConfiguration configuration)
    {
        // Use the existing framework service registration that includes IAppEnvironment
        services.AddEndatixFrameworkServices();

        // Add additional core services as needed
        services.AddOptions();
        services.AddHealthChecks();
    }

    private static void RegisterIdentityServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register identity services
    }
}