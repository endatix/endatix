using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Features.Submissions;
using Endatix.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Builders;

/// <summary>
/// Builder for configuring Endatix infrastructure components.
/// </summary>
public class InfrastructureBuilder
{
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

    public InfrastructureBuilder(IServiceCollection services, IConfiguration configuration)
    {
        Services = services;
        Configuration = configuration;

        Data = new InfrastructureDataBuilder(this);
        Identity = new InfrastructureIdentityBuilder(this);
        Messaging = new InfrastructureMessagingBuilder(this);
        Integrations = new InfrastructureIntegrationsBuilder(this);
    }

    /// <summary>
    /// Configures infrastructure with default settings.
    /// </summary>
    public InfrastructureBuilder UseDefaults()
    {
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

        return this;
    }

    private void ConfigureDefaultOptions()
    {
        Services.AddOptions<DataOptions>()
            .BindConfiguration(DataOptions.SECTION_NAME)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        Services.AddOptions<SubmissionOptions>()
            .BindConfiguration(SubmissionOptions.SECTION_NAME)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    internal IServiceCollection Services { get; }
    internal IConfiguration Configuration { get; }
}