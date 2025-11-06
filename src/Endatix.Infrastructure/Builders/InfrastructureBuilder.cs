using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Infrastructure;
using Endatix.Framework.Hosting;
using Endatix.Infrastructure.Features.Submissions;
using Endatix.Infrastructure.Multitenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Builders;

/// <summary>
/// Builder for configuring Endatix infrastructure components.
/// </summary>
public class InfrastructureBuilder
{
    private readonly ILogger _logger;
    private readonly IBuilderRoot _parentBuilder;

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    internal IServiceCollection Services => _parentBuilder.Services;

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    internal IConfiguration Configuration => _parentBuilder.Configuration;

    /// <summary>
    /// Gets the logger factory.
    /// </summary>
    internal ILoggerFactory LoggerFactory => _parentBuilder.LoggerFactory;

    /// <summary>
    /// Gets the application environment.
    /// </summary>
    internal IAppEnvironment? AppEnvironment => _parentBuilder.AppEnvironment;

    /// <summary>
    /// Gets the data builder for configuring data access.
    /// </summary>
    public InfrastructureDataBuilder Data { get; }

    /// <summary>
    /// Gets the security builder for configuring security services.
    /// </summary>
    public InfrastructureSecurityBuilder Security { get; }

    /// <summary>
    /// Gets the messaging builder for configuring messaging services.
    /// </summary>
    public InfrastructureMessagingBuilder Messaging { get; }

    /// <summary>
    /// Gets the integrations builder for configuring external integrations.
    /// </summary>
    public InfrastructureIntegrationsBuilder Integrations { get; }

    /// <summary>
    /// Initializes a new instance of the InfrastructureBuilder.
    /// </summary>
    /// <param name="parentBuilder">The root builder.</param>
    public InfrastructureBuilder(IBuilderRoot parentBuilder)
    {
        _parentBuilder = parentBuilder;

        // Create logger with the non-null LoggerFactory
        _logger = LoggerFactory.CreateLogger<InfrastructureBuilder>();

        Data = new InfrastructureDataBuilder(this);
        Security = new InfrastructureSecurityBuilder(this);
        Messaging = new InfrastructureMessagingBuilder(this);
        Integrations = new InfrastructureIntegrationsBuilder(this);
    }

    /// <summary>
    /// Configures infrastructure with default settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public InfrastructureBuilder UseDefaults()
    {
        LogSetupInfo("Configuring infrastructure with default settings");

        // Configure core infrastructure services
        Services.AddHttpContextAccessor();
        Services.AddWebHookProcessing();
        Services.AddMultitenancyConfiguration();
        Services.AddScoped<ISubmissionTokenService, SubmissionTokenService>();
        Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        Data.UseDefaults();
        Messaging.UseDefaults();
        Security.UseDefaults();
        Integrations.UseDefaults();

        LogSetupInfo("Infrastructure configured successfully");
        return this;
    }

    /// <summary>
    /// Logs setup information with a consistent prefix.
    /// </summary>
    /// <param name="message">The message to log.</param>
    internal void LogSetupInfo(string message)
    {
        _logger.LogInformation("[Infrastructure Setup] {Message}", message);
    }

    /// <summary>
    /// Builds and returns the root builder.
    /// </summary>
    /// <returns>The root builder.</returns>
    public IBuilderRoot Build()
    {
        // Call Build() on all child builders to ensure their configuration is applied
        Security.Build();
        Messaging.Build();
        
        LogSetupInfo("Infrastructure build completed");
        return _parentBuilder;
    }
}