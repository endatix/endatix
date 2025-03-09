using Endatix.Framework.Hosting;
using Endatix.Infrastructure.Logging;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Builder for configuring logging in the Endatix application.
/// </summary>
public class EndatixLoggingBuilder
{
    private const string LOGGER_OUTPUT_TEMPLATE = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";
    private readonly EndatixBuilder? _parentBuilder;
    private readonly IAppEnvironment? _appEnvironment;
    private bool _configuredLoggerRegistered;
    private LoggerConfiguration? _bootstrapLoggerConfiguration;
    private Action<LoggerConfiguration>? _configureCallback;
    private ILoggerFactory? _loggerFactory;
    private readonly ILogger<EndatixLoggingBuilder>? _logger;

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    public IConfiguration Configuration { get; }

    /// <summary>
    /// Gets the configured logger factory.
    /// </summary>
    internal ILoggerFactory LoggerFactory => _loggerFactory ??
        (_parentBuilder != null ? _parentBuilder.LoggerFactory :
            throw new InvalidOperationException("Logger factory not initialized. It should have been created in the constructor."));

    /// <summary>
    /// Initializes a new instance of the EndatixLoggingBuilder class with a parent builder.
    /// </summary>
    /// <param name="parentBuilder">The parent builder.</param>
    internal EndatixLoggingBuilder(EndatixBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
        Services = parentBuilder.Services;
        Configuration = parentBuilder.Configuration;
        _appEnvironment = parentBuilder.AppEnvironment;

        // Get the existing logger factory from the parent
        _loggerFactory = parentBuilder.LoggerFactory;
        _configuredLoggerRegistered = false;

        // Create a logger for this builder
        _logger = _loggerFactory.CreateLogger<EndatixLoggingBuilder>();

        // If parent already has a logger factory, we don't need to create a bootstrap logger
        _logger.LogInformation("EndatixLoggingBuilder initialized with existing logger factory");
    }

    /// <summary>
    /// Initializes a new instance of the EndatixLoggingBuilder class with services and configuration.
    /// Automatically creates a bootstrap logger if one doesn't exist.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    public EndatixLoggingBuilder(IServiceCollection services, IConfiguration configuration)
    {
        _parentBuilder = null;
        Services = services;
        Configuration = configuration;
        _configuredLoggerRegistered = false;

        // Try to get environment info from services
        // Note: This will only work if environment has been registered before this constructor is called
        var serviceProvider = services.BuildServiceProvider();
        _appEnvironment = serviceProvider.GetService<IAppEnvironment>();

        // Create bootstrap logger immediately
        InitializeBootstrapLogger();

        _logger = _loggerFactory!.CreateLogger<EndatixLoggingBuilder>();
    }

    /// <summary>
    /// Initializes the bootstrap logger and creates a logger factory.
    /// This is called automatically by the constructor.
    /// </summary>
    private void InitializeBootstrapLogger()
    {
        if (_loggerFactory != null)
        {
            return;
        }

        if (Log.Logger == Logger.None)
        {
            _bootstrapLoggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .WriteTo.Console(
                    applyThemeToRedirectedOutput: true,
                    outputTemplate: LOGGER_OUTPUT_TEMPLATE,
                    theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Sixteen);

            // Apply environment-specific configuration
            if (_appEnvironment?.IsDevelopment() == true)
            {
                _bootstrapLoggerConfiguration
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Debug);

                Log.Debug("Configuring bootstrap logger for development environment");
            }
            else if (_appEnvironment != null)
            {
                Log.Debug("Configuring bootstrap logger for {Environment} environment",
                    _appEnvironment.EnvironmentName);
            }

            Log.Logger = _bootstrapLoggerConfiguration.CreateBootstrapLogger();
        }

        _loggerFactory = new SerilogLoggerFactory(Log.Logger, dispose: false);

        Log.Information("Logger factory created for bootstrap logger");
    }

    /// <summary>
    /// Registers the logger provider to be configured when the host is built.
    /// This will replace the bootstrap logger with a fully configured one.
    /// </summary>
    /// <param name="useSerilog">Whether to use Serilog as the logger provider. Default is true.</param>
    /// <returns>The logging builder for chaining.</returns>
    public EndatixLoggingBuilder RegisterConfiguredLogger(bool useSerilog = true)
    {
        if (_configuredLoggerRegistered)
        {
            return this;
        }

        if (useSerilog)
        {
            // Use the implementation from Infrastructure
            SerilogConfigurator.ConfigureSerilog(Services, Configuration, _configureCallback);
            _logger?.LogInformation("Serilog configured and registered as the logging provider");
        }
        else
        {
            // Original generic approach
            Services.AddLogging(builder =>
            {
                builder.ClearProviders();

                _logger?.LogInformation("Logging configuration registered - host will configure the full logger");
            });
        }

        _configuredLoggerRegistered = true;
        return this;
    }

    /// <summary>
    /// Gets the logger factory created by this builder.
    /// </summary>
    /// <returns>The logger factory.</returns>
    public ILoggerFactory GetLoggerFactory()
    {
        return _loggerFactory!;
    }

    /// <summary>
    /// Gets the components created by this builder when used in standalone mode.
    /// </summary>
    /// <returns>A tuple containing the logger factory.</returns>
    internal ILoggerFactory GetComponents()
    {
        return _loggerFactory!;
    }

    /// <summary>
    /// Configures logging with default settings.
    /// This registers Serilog with default configuration.
    /// </summary>
    /// <returns>The logging builder for chaining.</returns>
    public EndatixLoggingBuilder UseDefaults()
    {
        // Register the fully configured logger
        RegisterConfiguredLogger();

        return this;
    }

    /// <summary>
    /// Configures Application Insights for logging.
    /// </summary>
    /// <returns>The logging builder for chaining.</returns>
    public EndatixLoggingBuilder UseApplicationInsights()
    {
        _logger?.LogInformation("Configuring Application Insights...");

        // Configure Application Insights with default settings
        Services.AddApplicationInsightsTelemetry(options =>
        {
            options.EnableAdaptiveSampling = true;
            options.EnableQuickPulseMetricStream = true;
        });

        _logger?.LogInformation("Application Insights configured successfully");
        return this;
    }

    /// <summary>
    /// Configures Application Insights with custom settings.
    /// </summary>
    /// <param name="configure">Action to configure Application Insights.</param>
    /// <returns>The logging builder for chaining.</returns>
    public EndatixLoggingBuilder UseApplicationInsights(Action<ApplicationInsightsServiceOptions> configure)
    {
        _logger?.LogInformation("Configuring Application Insights with custom settings...");

        // Configure Application Insights with custom settings
        Services.AddApplicationInsightsTelemetry(configure);

        _logger?.LogInformation("Application Insights configured successfully with custom settings");
        return this;
    }

    /// <summary>
    /// Configures the bootstrap logger with custom settings before the host is built.
    /// This must be called before the host is built and before a bootstrap logger is created.
    /// </summary>
    /// <param name="configure">Action to configure the bootstrap logger.</param>
    /// <returns>The logging builder for chaining.</returns>
    public EndatixLoggingBuilder ConfigureBootstrapLogger(Action<LoggerConfiguration> configure)
    {
        if (Log.Logger != Logger.None)
        {
            _logger?.LogWarning("Bootstrap logger already exists - cannot configure it after creation");
            return this;
        }

        _bootstrapLoggerConfiguration ??= new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information);

        configure(_bootstrapLoggerConfiguration);
        return this;
    }

    /// <summary>
    /// Customizes Serilog configuration beyond what's automatically configured.
    /// This configuration will be applied when the host is built.
    /// </summary>
    /// <param name="configure">Action to customize Serilog configuration.</param>
    /// <returns>The logging builder for chaining.</returns>
    public EndatixLoggingBuilder ConfigureSerilog(Action<LoggerConfiguration> configure)
    {
        _logger?.LogInformation("Custom Serilog configuration will be applied when the host is built");

        _configureCallback = configure;

        // Ensure the configured logger is registered
        RegisterConfiguredLogger();

        return this;
    }

    /// <summary>
    /// Gets the parent builder.
    /// </summary>
    /// <returns>The parent builder.</returns>
    public EndatixBuilder Build()
    {
        if (_parentBuilder == null)
        {
            throw new InvalidOperationException("This builder was not created with a parent builder. Use GetComponents() instead.");
        }

        return _parentBuilder;
    }

    /// <summary>
    /// Creates a logger for the specified category.
    /// </summary>
    /// <typeparam name="T">The category class.</typeparam>
    /// <returns>A logger instance.</returns>
    internal ILogger<T> CreateLogger<T>()
    {
        return _loggerFactory!.CreateLogger<T>();
    }
}