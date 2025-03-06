using Endatix.Infrastructure.Builders;
using Endatix.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Builder for configuring Endatix messaging features.
/// </summary>
public class EndatixMessagingBuilder
{
    private readonly EndatixBuilder _parentBuilder;
    private readonly ILogger? _logger;
    private readonly InfrastructureMessagingBuilder _infrastructureMessagingBuilder;

    /// <summary>
    /// Initializes a new instance of the EndatixMessagingBuilder class.
    /// </summary>
    /// <param name="parentBuilder">The parent builder.</param>
    internal EndatixMessagingBuilder(EndatixBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
        _logger = parentBuilder.LoggerFactory?.CreateLogger<EndatixMessagingBuilder>();
        
        // Get or create the infrastructure builder
        var infrastructureBuilder = parentBuilder.Infrastructure;
        _infrastructureMessagingBuilder = infrastructureBuilder.Messaging;
    }
    
    /// <summary>
    /// Configures messaging with default settings.
    /// </summary>
    /// <returns>The messaging builder for chaining.</returns>
    public EndatixMessagingBuilder UseDefaults()
    {
        LogSetupInfo("Configuring messaging with default settings");
        
        // Use infrastructure messaging builder with defaults
        _infrastructureMessagingBuilder.UseDefaults();

        LogSetupInfo("Messaging configuration completed");
        return this;
    }
    
    /// <summary>
    /// Configures messaging with custom settings.
    /// </summary>
    public EndatixMessagingBuilder Configure(Action<MediatRConfigOptions> configure)
    {
        LogSetupInfo("Configuring messaging with custom settings");
        
        // Use infrastructure messaging builder with custom options
        _infrastructureMessagingBuilder.Configure(configure);

        LogSetupInfo("Messaging configuration completed");
        return this;
    }
    
    /// <summary>
    /// Returns to the parent builder.
    /// </summary>
    /// <returns>The parent builder for chaining.</returns>
    public EndatixBuilder Build() => _parentBuilder;
    
    private void LogSetupInfo(string message)
    {
        _logger?.LogInformation("[Messaging Setup] {Message}", message);
    }
} 