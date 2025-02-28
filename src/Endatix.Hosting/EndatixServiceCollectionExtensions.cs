using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.Extensions.Logging;
using Ardalis.GuardClauses;
using Endatix.Hosting.Builders;
using Endatix.Hosting.Core;
using Endatix.Hosting.Internal;
using Logging = Microsoft.Extensions.Logging;

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
        // Register core services
        services.AddEndatixFrameworkServices();
        
        // Add logging services
        services.AddLogging();
        
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
        services.AddEndatix(configuration).UseDefaults();
        return services;
    }
    
    /// <summary>
    /// Creates and configures an instance of IEndatixApp.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>An instance of IEndatixApp.</returns>
    public static IEndatixApp CreateEndatix(this WebApplicationBuilder builder, Logging.ILogger? logger = null)
    {
        Guard.Against.Null(builder);

        logger ??= CreateSerilogLogger();
        var endatixWebApp = new EndatixWebApp(logger, builder);
        endatixWebApp.LogSetupInformation("Starting Endatix Web Application Host");

        builder.Services.AddEndatixFrameworkServices();

        return endatixWebApp;
    }
    
    /// <summary>
    /// Creates a Serilog logger.
    /// </summary>
    /// <returns>A logger instance.</returns>
    private static Logging.ILogger CreateSerilogLogger()
    {
        var serilogLogger = new LoggerConfiguration()
                        .Enrich.FromLogContext()
                        .WriteTo.Async(wt => wt.Console(theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Sixteen, applyThemeToRedirectedOutput: true))
                        .CreateLogger();

        return new SerilogLoggerFactory(serilogLogger)
            .CreateLogger(nameof(EndatixWebApp));
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
} 