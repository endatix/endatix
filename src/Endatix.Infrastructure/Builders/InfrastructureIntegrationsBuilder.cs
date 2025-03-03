using Endatix.Core.Abstractions;
using Endatix.Core.Features.Email;
using Endatix.Infrastructure.Email;
using Endatix.Infrastructure.Integrations.Slack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Builders;

/// <summary>
/// Builder for configuring Endatix external integrations.
/// </summary>
public class InfrastructureIntegrationsBuilder
{
    private readonly InfrastructureBuilder _parentBuilder;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the InfrastructureIntegrationsBuilder class.
    /// </summary>
    /// <param name="parentBuilder">The parent infrastructure builder.</param>
    internal InfrastructureIntegrationsBuilder(InfrastructureBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
        _logger = parentBuilder.Services.BuildServiceProvider().GetService<ILoggerFactory>()?.CreateLogger("Endatix.Setup");
    }

    /// <summary>
    /// Configures integrations with default settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public InfrastructureIntegrationsBuilder UseDefaults()
    {
        LogSetupInfo("Configuring integrations with default settings");
        
        // Configure default email provider
        _parentBuilder.Services.AddEmailSender<SendGridEmailSender, SendGridSettings>();
        
        // Configure default Slack integration
        _parentBuilder.Services.AddSlackConfiguration<SlackSettings>();
        
        LogSetupInfo("Integrations configuration completed");
        return this;
    }

    /// <summary>
    /// Adds email provider configuration.
    /// </summary>
    /// <typeparam name="TEmailSender">Type of email sender.</typeparam>
    /// <typeparam name="TSettings">Type of settings.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public InfrastructureIntegrationsBuilder AddEmail<TEmailSender, TSettings>()
        where TEmailSender : class, IEmailSender, IHasConfigSection<TSettings>
        where TSettings : class, new()
    {
        LogSetupInfo($"Adding email provider: {typeof(TEmailSender).Name}");
        _parentBuilder.Services.AddEmailSender<TEmailSender, TSettings>();
        return this;
    }

    /// <summary>
    /// Adds Slack integration configuration.
    /// </summary>
    /// <typeparam name="TSettings">Type of settings.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public InfrastructureIntegrationsBuilder AddSlack<TSettings>() 
        where TSettings : class, new()
    {
        LogSetupInfo($"Adding Slack integration with settings: {typeof(TSettings).Name}");
        _parentBuilder.Services.AddSlackConfiguration<TSettings>();
        return this;
    }

    /// <summary>
    /// Returns to the parent infrastructure builder.
    /// </summary>
    /// <returns>The parent infrastructure builder.</returns>
    public InfrastructureBuilder Parent() => _parentBuilder;

    private void LogSetupInfo(string message)
    {
        _logger?.LogInformation("[Integrations Setup] {Message}", message);
    }
} 