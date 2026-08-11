using System.Diagnostics.CodeAnalysis;
using Endatix.Framework.Hosting;
using Endatix.Hosting.Builders.Logging;
using Endatix.Hosting.Logging;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Builder for configuring logging in the Endatix application.
/// </summary>
/// <remarks>
/// Providers only. The OTLP <em>export</em> of log records is registered by
/// <see cref="EndatixTelemetryBuilder"/> alongside metrics and traces, so all three signals share one
/// resolved endpoint and one resource. This builder owns the provider list and the consumer hook.
/// </remarks>
public class EndatixLoggingBuilder
{
    internal const string LegacySerilogSection = "Serilog";
    internal const string LoggingSection = "Logging";
    [SuppressMessage("csharpsquid", "S1075:URIs should not be hardcoded",
        Justification = "A documentation link shown in a warning message. It does not vary by "
                      + "environment, and making it configurable would let an operator break the "
                      + "help text without noticing.")]
    private const string MigrationDocsUrl = "https://docs.endatix.com/docs/configuration/observability";

    private readonly EndatixBuilder? _parentBuilder;
    private readonly IAppEnvironment? _appEnvironment;

    private bool _configuredLoggerRegistered;
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

        _loggerFactory = parentBuilder.LoggerFactory;
        _configuredLoggerRegistered = false;

        _logger = _loggerFactory.CreateLogger<EndatixLoggingBuilder>();
    }

    /// <summary>
    /// Initializes a new instance of the EndatixLoggingBuilder class with services and configuration.
    /// Automatically creates a startup logger if one doesn't exist.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    public EndatixLoggingBuilder(IServiceCollection services, IConfiguration configuration)
    {
        _parentBuilder = null;
        Services = services;
        Configuration = configuration;
        _configuredLoggerRegistered = false;

        // Note: this will only work if environment has been registered before this constructor runs.
        var serviceProvider = services.BuildServiceProvider();
        _appEnvironment = serviceProvider.GetService<IAppEnvironment>();

        InitializeStartupLogger();

        _logger = _loggerFactory!.CreateLogger<EndatixLoggingBuilder>();
    }

    /// <summary>
    /// Creates the standalone factory used for builder diagnostics before the host's own logging
    /// pipeline exists. Replaces the former Serilog bootstrap logger.
    /// </summary>
    /// <remarks>
    /// Deliberately separate from the DI pipeline: builder code logs during
    /// <see cref="EndatixBuilder"/> construction, long before a service provider can be built.
    /// </remarks>
    private void InitializeStartupLogger()
    {
        if (_loggerFactory != null)
        {
            return;
        }

        _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(logging =>
        {
            logging.AddConfiguration(Configuration.GetSection(LoggingSection));
            logging.AddConsole();

            if (_appEnvironment?.IsDevelopment() == true)
            {
                logging.SetMinimumLevel(LogLevel.Debug);
            }
        });
    }

    /// <summary>
    /// Registers the Endatix logging baseline on the service collection: providers cleared, then
    /// Console, then any <see cref="Configure"/> callbacks.
    /// </summary>
    /// <returns>The logging builder for chaining.</returns>
    public EndatixLoggingBuilder RegisterConfiguredLogger()
    {
        if (_configuredLoggerRegistered)
        {
            return this;
        }

        WarnOnLegacySerilogSection();

        Services.AddLogging(logging =>
        {
            // WebApplication.CreateBuilder has already added Console, Debug, EventSource and
            // EventLog. Not clearing them means duplicated console output and, on Windows, EventLog
            // noise. Clearing is why the Configure hook below exists at all.
            logging.ClearProviders();
            logging.AddConfiguration(Configuration.GetSection(LoggingSection));

            // Always registered, unconditionally: this is what `kubectl logs` and `docker logs`
            // read. A host with no OTLP endpoint must still produce output.
            logging.AddConsole();

            // Added after the console and never in place of it, so enabling files cannot take a
            // deployed host's stdout away. No-op unless Endatix:Logging:File:Enabled is set.
            logging.AddEndatixFileLogging(Configuration);
        });

        _configuredLoggerRegistered = true;
        _logger?.LogLoggingConfigured();

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
    /// Configures logging with default settings: console output, plus OTLP export when
    /// <see cref="EndatixTelemetryBuilder"/> resolves an endpoint.
    /// </summary>
    /// <returns>The logging builder for chaining.</returns>
    public EndatixLoggingBuilder UseDefaults()
    {
        RegisterConfiguredLogger();

        return this;
    }

    /// <summary>
    /// Adds custom logging configuration on top of the Endatix baseline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs <em>after</em> Endatix has cleared providers and added Console, so a provider added here
    /// survives. Anything registered on <c>builder.Logging</c> directly does not — Endatix clears it.
    /// </para>
    /// <para>Composes: repeated calls apply in call order, each on top of the last.</para>
    /// <example>
    /// <code>
    /// endatix.Logging.Configure(logging => logging.AddAzureWebAppDiagnostics());
    /// </code>
    /// </example>
    /// </remarks>
    /// <param name="configure">Action applied to the logging builder.</param>
    /// <returns>The logging builder for chaining.</returns>
    public EndatixLoggingBuilder Configure(Action<ILoggingBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        // Baseline first, then the callback on top. AddLogging invokes its argument immediately
        // rather than at provider-build time, so ordering here is literally call order: anything
        // registered before RegisterConfiguredLogger() would be wiped by its ClearProviders().
        RegisterConfiguredLogger();
        Services.AddLogging(configure);

        return this;
    }

    /// <summary>
    /// Configures Application Insights for logging.
    /// </summary>
    /// <returns>The logging builder for chaining.</returns>
    public EndatixLoggingBuilder UseApplicationInsights()
    {
        // Baseline first, for the same reason as Configure(): AddApplicationInsightsTelemetry
        // registers an ILoggerProvider, and RegisterConfiguredLogger()'s ClearProviders() would
        // remove it if it ran afterwards.
        RegisterConfiguredLogger();

        Services.AddApplicationInsightsTelemetry(options =>
        {
            options.EnableAdaptiveSampling = true;
            options.EnableQuickPulseMetricStream = true;
        });

        return this;
    }

    /// <summary>
    /// Configures Application Insights with custom settings.
    /// </summary>
    /// <param name="configure">Action to configure Application Insights.</param>
    /// <returns>The logging builder for chaining.</returns>
    public EndatixLoggingBuilder UseApplicationInsights(Action<ApplicationInsightsServiceOptions> configure)
    {
        RegisterConfiguredLogger();

        Services.AddApplicationInsightsTelemetry(configure);

        return this;
    }

    /// <summary>
    /// Warns once when a host still carries a Serilog configuration section. Serilog no longer reads
    /// it, so leaving it in place silently changes the effective log levels.
    /// </summary>
    private void WarnOnLegacySerilogSection()
    {
        if (!Configuration.GetSection(LegacySerilogSection).Exists())
        {
            return;
        }

        _logger?.LogLegacySerilogSectionDetected(LegacySerilogSection, LoggingSection, MigrationDocsUrl);
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
