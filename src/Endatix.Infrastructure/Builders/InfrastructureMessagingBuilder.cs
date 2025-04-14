using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Infrastructure.Messaging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Builders;

/// <summary>
/// Builder for configuring Endatix messaging infrastructure.
/// </summary>
public class InfrastructureMessagingBuilder
{
    // Default configuration action - defined once and reused
    private static readonly Action<MediatRConfigOptions> _defaultConfigAction = options =>
    {
        options.IncludeLoggingPipeline = true;
    };

    private readonly InfrastructureBuilder _parentBuilder;
    private readonly ILogger _logger;

    // Store the configuration to be applied when Build() is called
    private Action<MediatRConfigOptions>? _configureAction;
    private bool _configured;

    /// <summary>
    /// Initializes a new instance of the InfrastructureMessagingBuilder class.
    /// </summary>
    /// <param name="parentBuilder">The parent infrastructure builder.</param>
    internal InfrastructureMessagingBuilder(InfrastructureBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
        _logger = parentBuilder.LoggerFactory.CreateLogger<InfrastructureMessagingBuilder>();
    }

    /// <summary>
    /// Configures messaging with default settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public InfrastructureMessagingBuilder UseDefaults()
    {
        LogSetupInfo("Using default messaging configuration");
        _configureAction = _defaultConfigAction;
        return this;
    }

    /// <summary>
    /// Configures messaging with custom settings.
    /// </summary>
    /// <param name="configure">Action to configure messaging options.</param>
    /// <returns>The builder for chaining.</returns>
    public InfrastructureMessagingBuilder Configure(Action<MediatRConfigOptions> configure)
    {
        Guard.Against.Null(configure);

        LogSetupInfo("Storing custom messaging configuration");

        if (_configureAction == null)
        {
            // If no previous configuration, just store this one
            _configureAction = configure;
        }
        else
        {
            // If there was a previous configuration (e.g., from UseDefaults),
            // compose a new action that applies both in sequence
            var previousAction = _configureAction;
            _configureAction = options =>
            {
                previousAction(options);
                configure(options);
            };
        }

        return this;
    }

    /// <summary>
    /// Builds and returns the parent infrastructure builder.
    /// This method applies the configuration if it hasn't been applied yet.
    /// </summary>
    /// <returns>The parent infrastructure builder.</returns>
    public InfrastructureBuilder Build()
    {
        if (!_configured)
        {
            LogSetupInfo("Applying messaging configuration");

            // If no configuration was provided, use defaults
            var configAction = _configureAction ?? _defaultConfigAction;

            _parentBuilder.Services.AddMediatRMessaging(configAction);
            _configured = true;

            LogSetupInfo("Messaging configuration applied");
        }

        return _parentBuilder;
    }

    private void LogSetupInfo(string message)
    {
        _logger.LogDebug("[Messaging Setup] {Message}", message);
    }
}