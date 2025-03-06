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
    /// <param name="parentBuilder">The root builder.</param>
    public InfrastructureBuilder(IBuilderRoot parentBuilder)
    {
        _parentBuilder = parentBuilder;

        // Create logger with the non-null LoggerFactory
        _logger = LoggerFactory.CreateLogger<InfrastructureBuilder>();

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
        _logger.LogInformation("[Infrastructure Setup] {Message}", message);
    }

    /// <summary>
    /// Builds and returns the root builder.
    /// </summary>
    /// <returns>The root builder.</returns>
    public IBuilderRoot Build() => _parentBuilder;
}