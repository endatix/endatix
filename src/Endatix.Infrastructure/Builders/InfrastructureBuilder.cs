using Endatix.Core.Abstractions;
using Endatix.Framework.Hosting;
using Endatix.Infrastructure.Features.Submissions;
using Endatix.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Builders;

/// <summary>
/// Builder for configuring Endatix infrastructure components.
/// </summary>
public class InfrastructureBuilder
{
    private readonly ILogger? _logger;
    private readonly IBuilderParent _parentBuilder;

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    internal IServiceCollection Services { get; }

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    internal IConfiguration Configuration { get; }

    /// <summary>
    /// Gets the logger factory.
    /// </summary>
    internal ILoggerFactory? LoggerFactory { get; }

    /// <summary>
    /// Gets the application environment.
    /// </summary>
    internal IAppEnvironment? AppEnvironment { get; }

    /// <summary>
    /// Gets the data builder for configuring data access.
    /// </summary>
    public InfrastructureDataBuilder Data { get; }

    /// <summary>
    /// Gets the identity builder for configuring identity services.
    /// </summary>
    public InfrastructureIdentityBuilder Identity { get; }

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
    /// <param name="parentBuilder">The parent builder.</param>
    public InfrastructureBuilder(IBuilderParent parentBuilder)
    {
        _parentBuilder = parentBuilder;
        
        // Get properties from the parent builder via the interface
        Services = parentBuilder.Services;
        Configuration = parentBuilder.Configuration;
        AppEnvironment = parentBuilder.AppEnvironment;
        LoggerFactory = parentBuilder.LoggerFactory;
        
        if (LoggerFactory != null)
        {
            _logger = LoggerFactory.CreateLogger<InfrastructureBuilder>();
        }

        Data = new InfrastructureDataBuilder(this);
        Identity = new InfrastructureIdentityBuilder(this);
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

        Data.UseDefaults();
        Messaging.UseDefaults();
        Identity.UseDefaults();
        Integrations.UseDefaults();

        Services.AddScoped(typeof(ISubmissionTokenService), typeof(SubmissionTokenService));

        // Add default config options
        ConfigureDefaultOptions();

        LogSetupInfo("Infrastructure configured successfully");
        return this;
    }

    private void ConfigureDefaultOptions()
    {
        Services.AddOptions<DataOptions>()
            .BindConfiguration(DataOptions.SECTION_NAME)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    /// <summary>
    /// Logs setup information with a consistent prefix.
    /// </summary>
    /// <param name="message">The message to log.</param>
    internal void LogSetupInfo(string message)
    {
        _logger?.LogInformation("[Infrastructure Setup] {Message}", message);
    }
    
    /// <summary>
    /// Returns to the parent builder.
    /// </summary>
    /// <returns>The parent builder.</returns>
    public IBuilderParent Parent() => _parentBuilder;
}