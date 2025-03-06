using Endatix.Core.Infrastructure.Messaging;
using Endatix.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Builders;

/// <summary>
/// Builder for configuring Endatix messaging infrastructure.
/// </summary>
public class InfrastructureMessagingBuilder
{
    private readonly InfrastructureBuilder _parentBuilder;
    private readonly ILogger _logger;

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
        LogSetupInfo("Configuring messaging with default settings");

        _parentBuilder.Services.AddMediatRMessaging(options =>
        {
            options.IncludeLoggingPipeline = true;
        });

        LogSetupInfo("Messaging configuration completed");
        return this;
    }

    /// <summary>
    /// Configures messaging with custom settings.
    /// </summary>
    /// <param name="configure">Action to configure messaging options.</param>
    /// <returns>The builder for chaining.</returns>
    public InfrastructureMessagingBuilder Configure(Action<MediatRConfigOptions> configure)
    {
        LogSetupInfo("Configuring messaging with custom settings");

        _parentBuilder.Services.AddMediatRMessaging(configure);

        LogSetupInfo("Messaging configuration completed");
        return this;
    }

    /// <summary>
    /// Builds and returns the parent infrastructure builder.
    /// </summary>
    /// <returns>The parent infrastructure builder.</returns>
    public InfrastructureBuilder Build() => _parentBuilder;

    private void LogSetupInfo(string message)
    {
        _logger.LogInformation("[Messaging Setup] {Message}", message);
    }
}