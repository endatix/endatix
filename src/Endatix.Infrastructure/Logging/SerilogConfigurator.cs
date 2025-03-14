using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;

namespace Endatix.Infrastructure.Logging;

/// <summary>
/// Provides methods to configure Serilog within the application.
/// </summary>
public static class SerilogConfigurator
{
    /// <summary>
    /// Configures Serilog with the application's configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="customConfig">Optional action to apply additional custom configuration.</param>
    public static void ConfigureSerilog(
        IServiceCollection services,
        IConfiguration configuration,
        Action<LoggerConfiguration>? customConfig = null)
    {
        services.AddSerilog((serviceProvider, loggerConfiguration) =>
        {
            // Configure from appsettings.json
            loggerConfiguration.ReadFrom.Configuration(configuration);

            // Pull in service-driven configuration
            loggerConfiguration.ReadFrom.Services(serviceProvider);
            
            // Add standard enrichers
            loggerConfiguration.Enrich.FromLogContext();

            // Apply custom configuration if provided
            customConfig?.Invoke(loggerConfiguration);
        });
    }
} 