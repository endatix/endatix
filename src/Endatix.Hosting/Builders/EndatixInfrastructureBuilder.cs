using Endatix.Infrastructure.Builders;
using Microsoft.Extensions.Logging;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Builder for configuring Endatix infrastructure components (data access, identity, 
/// messaging, and integrations).
/// </summary>
public class EndatixInfrastructureBuilder
{
    private readonly EndatixBuilder _parentBuilder;
    private readonly InfrastructureBuilder _infrastructureBuilder;
    private readonly ILogger<EndatixInfrastructureBuilder>? _logger;

    /// <summary>
    /// Builder for data-related infrastructure (repositories, entity configurations).
    /// </summary>
    public InfrastructureDataBuilder Data => _infrastructureBuilder.Data;

    /// <summary>
    /// Builder for identity services (authentication, authorization, user management).
    /// </summary>
    public InfrastructureIdentityBuilder Identity => _infrastructureBuilder.Identity;

    /// <summary>
    /// Builder for messaging services (MediatR, event dispatching, handling pipelines).
    /// </summary>
    public InfrastructureMessagingBuilder Messaging => _infrastructureBuilder.Messaging;

    /// <summary>
    /// Builder for external integrations (APIs, webhooks, external dependencies).
    /// </summary>
    public InfrastructureIntegrationsBuilder Integrations => _infrastructureBuilder.Integrations;

    /// <summary>
    /// Initializes a new instance of the EndatixInfrastructureBuilder class.
    /// </summary>
    /// <param name="parentBuilder">The parent Endatix builder.</param>
    internal EndatixInfrastructureBuilder(EndatixBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
        _logger = parentBuilder.LoggerFactory?.CreateLogger<EndatixInfrastructureBuilder>();

        // Create the actual infrastructure builder and delegate to it
        _infrastructureBuilder = new InfrastructureBuilder(parentBuilder);

        LogSetupInfo("Infrastructure builder initialized");
    }

    /// <summary>
    /// Configures all infrastructure components with sensible defaults.
    /// Sets up data access, identity, messaging, and integrations.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixInfrastructureBuilder UseDefaults()
    {
        LogSetupInfo("Configuring infrastructure with default settings");

        _infrastructureBuilder.UseDefaults();

        LogSetupInfo("Infrastructure configuration completed");
        return this;
    }

    /// <summary>
    /// Completes configuration and returns to the parent builder.
    /// </summary>
    /// <returns>The parent builder.</returns>
    public EndatixBuilder Build()
    {
        // Call Build() on the infrastructure builder to ensure all child builders are built
        _infrastructureBuilder.Build();

        return _parentBuilder;
    }

    private void LogSetupInfo(string message)
    {
        _logger?.LogDebug("[Infrastructure Setup] {Message}", message);
    }
}