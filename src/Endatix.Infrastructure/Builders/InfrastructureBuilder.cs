using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Abstractions;
using Endatix.Infrastructure.Features.Submissions;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Builders;

/// <summary>
/// Builder for configuring Endatix infrastructure components.
/// </summary>
public class InfrastructureBuilder
{
    private readonly IServiceCollection _services;
    private readonly IConfiguration _configuration;

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
        _services = services;
        _configuration = configuration;

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
        _services.AddHttpContextAccessor();
        _services.AddWebHookProcessing();

        this.Messaging.UseDefaults();
        this.Identity.UseDefaults();
        this.Data.UseDefaults();
        this.Integrations.UseDefaults();

        _services.AddScoped(typeof(ISubmissionTokenService), typeof(SubmissionTokenService));

        // Configure default options
        ConfigureDefaultOptions();

        return this;
    }

    private void ConfigureDefaultOptions()
    {
        _services.AddOptions<DataOptions>()
            .BindConfiguration(DataOptions.SECTION_NAME)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        _services.AddOptions<SubmissionOptions>()
            .BindConfiguration(SubmissionOptions.SECTION_NAME)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    internal IServiceCollection Services => _services;
    internal IConfiguration Configuration => _configuration;
}