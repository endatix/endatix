using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Endatix.Hosting.Options;
using Endatix.Hosting.Logging;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Main builder for configuring Endatix services.
/// </summary>
public class EndatixBuilder
{
    /// <summary>
    /// Gets the service collection.
    /// </summary>
    internal IServiceCollection Services { get; }

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    internal IConfiguration Configuration { get; }

    /// <summary>
    /// Gets the API builder.
    /// </summary>
    public EndatixApiBuilder Api { get; }

    /// <summary>
    /// Gets the persistence builder.
    /// </summary>
    public EndatixPersistenceBuilder Persistence { get; }

    /// <summary>
    /// Gets the security builder.
    /// </summary>
    public EndatixSecurityBuilder Security { get; }

    /// <summary>
    /// Gets the messaging builder.
    /// </summary>
    public EndatixMessagingBuilder Messaging { get; }

    /// <summary>
    /// Gets the logging builder.
    /// </summary>
    public EndatixLoggingBuilder Logging { get; }

    /// <summary>
    /// Gets a logger factory that can create loggers for specific categories.
    /// </summary>
    public ILoggerFactory? LoggerFactory => Logging.LoggerFactory;

    private EndatixSetupLogger? _setupLogger;

    /// <summary>
    /// Gets the setup logger for logging during configuration.
    /// </summary>
    internal EndatixSetupLogger SetupLogger 
    {
        get
        {
            if (_setupLogger == null)
            {
                var logger = LoggerFactory?.CreateLogger("Endatix.Setup") ?? 
                    throw new InvalidOperationException("Logger factory not initialized. Ensure logging is configured before using setup logging.");
                _setupLogger = new EndatixSetupLogger(logger);
            }
            return _setupLogger;
        }
    }

    /// <summary>
    /// Initializes a new instance of the EndatixBuilder class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    internal EndatixBuilder(IServiceCollection services, IConfiguration configuration)
    {
        Services = services;
        Configuration = configuration;

        // Initialize feature builders
        Logging = new EndatixLoggingBuilder(this);
        Api = new EndatixApiBuilder(this);
        Persistence = new EndatixPersistenceBuilder(this);
        Security = new EndatixSecurityBuilder(this);
        Messaging = new EndatixMessagingBuilder(this);
    }

    /// <summary>
    /// Configures Endatix with default settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixBuilder UseDefaults()
    {
        // Configure logging first
        Logging.UseDefaults();

        // Log the start of configuration
        SetupLogger.Information("Starting Endatix configuration with default settings");

        // Configure other features
        Api.UseDefaults();
        SetupLogger.Information("API configuration completed");

        Persistence.UseDefaults();
        SetupLogger.Information("Persistence configuration completed");

        Security.UseDefaults();
        SetupLogger.Information("Security configuration completed");

        Messaging.UseDefaults();
        SetupLogger.Information("Messaging configuration completed");

        // Configure telemetry if needed
        var options = new EndatixOptions();
        Configuration.GetSection("Endatix").Bind(options);

        if (options.IsAzure)
        {
            SetupLogger.Information("Configuring Azure Application Insights telemetry");
            Services.AddApplicationInsightsTelemetry();
        }

        SetupLogger.Information("Endatix configuration completed successfully");
        return this;
    }

    /// <summary>
    /// Configures Endatix with minimal settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixBuilder UseMinimalSetup()
    {
        // Configure only essential services
        Persistence.UseDefaults();

        return this;
    }

    /// <summary>
    /// Configures Endatix options.
    /// </summary>
    /// <param name="configure">Action to configure options.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixBuilder ConfigureOptions(Action<EndatixOptions> configure)
    {
        Services.Configure(configure);
        return this;
    }
}