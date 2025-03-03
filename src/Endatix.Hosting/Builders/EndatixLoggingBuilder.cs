using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Builder for configuring Endatix logging.
/// </summary>
public class EndatixLoggingBuilder
{
    private readonly EndatixBuilder _parentBuilder;
    private LoggerConfiguration? _serilogConfiguration;
    private bool _useDefaultLogger;

    /// <summary>
    /// Gets the configured logger factory.
    /// </summary>
    internal ILoggerFactory? LoggerFactory { get; private set; }

    /// <summary>
    /// Initializes a new instance of the EndatixLoggingBuilder class.
    /// </summary>
    /// <param name="parentBuilder">The parent builder.</param>
    internal EndatixLoggingBuilder(EndatixBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
        _useDefaultLogger = true; // Default to using Serilog
    }

    /// <summary>
    /// Configures logging with default settings using Serilog.
    /// </summary>
    /// <returns>The logging builder for chaining.</returns>
    public EndatixLoggingBuilder UseDefaults()
    {
        _useDefaultLogger = true;
        _serilogConfiguration = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Async(wt => wt.Console(
                theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Sixteen,
                applyThemeToRedirectedOutput: true
            ));

        ApplyLoggingConfiguration();
        return this;
    }

    /// <summary>
    /// Configures Serilog with custom configuration while keeping the default provider.
    /// </summary>
    /// <param name="configure">Action to configure Serilog.</param>
    /// <returns>The logging builder for chaining.</returns>
    public EndatixLoggingBuilder ConfigureSerilog(Action<LoggerConfiguration> configure)
    {
        _useDefaultLogger = true;
        _serilogConfiguration ??= new LoggerConfiguration();
        configure(_serilogConfiguration);
        ApplyLoggingConfiguration();
        return this;
    }

    /// <summary>
    /// Configures custom logging without using Serilog.
    /// </summary>
    /// <param name="configure">Action to configure logging.</param>
    /// <returns>The logging builder for chaining.</returns>
    public EndatixLoggingBuilder UseCustomLogging(Action<ILoggingBuilder> configure)
    {
        _useDefaultLogger = false;
        _serilogConfiguration = null;
        
        _parentBuilder.Services.AddLogging(builder =>
        {
            configure(builder);
        });
        
        return this;
    }

    /// <summary>
    /// Gets the parent builder.
    /// </summary>
    /// <returns>The parent builder.</returns>
    public EndatixBuilder Parent() => _parentBuilder;

    private void ApplyLoggingConfiguration()
    {
        if (_useDefaultLogger && _serilogConfiguration != null)
        {
            // Create Serilog logger
            var serilogLogger = _serilogConfiguration.CreateLogger();

            // Configure logging services
            _parentBuilder.Services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.ClearProviders();
                builder.AddSerilog(serilogLogger, dispose: true);
            });

            // Register Serilog specific services
            _parentBuilder.Services.AddSingleton(serilogLogger);
            LoggerFactory = new SerilogLoggerFactory(serilogLogger);
            _parentBuilder.Services.AddSingleton(LoggerFactory);
        }
    }

    /// <summary>
    /// Gets a logger for the specified category.
    /// </summary>
    /// <typeparam name="T">The category type.</typeparam>
    /// <returns>A logger instance.</returns>
    internal ILogger<T> CreateLogger<T>()
    {
        if (LoggerFactory == null)
        {
            UseDefaults(); // Ensure we have a logger factory
        }
        
        return LoggerFactory!.CreateLogger<T>();
    }
}