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
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the InfrastructureIntegrationsBuilder class.
    /// </summary>
    /// <param name="parentBuilder">The parent infrastructure builder.</param>
    internal InfrastructureIntegrationsBuilder(InfrastructureBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
        _logger = parentBuilder.LoggerFactory.CreateLogger<InfrastructureIntegrationsBuilder>();
    }

    /// <summary>
    /// Configures integrations with default settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public InfrastructureIntegrationsBuilder UseDefaults()
    {
        LogSetupInfo("Configuring integrations with default settings");

        _parentBuilder.Services.AddEmailTemplateSettings();
        _parentBuilder.Services.AddEmailSender<SmtpEmailSender, SmtpSettings>();

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
    /// Builds and returns the parent infrastructure builder.
    /// </summary>
    /// <returns>The parent infrastructure builder.</returns>
    public InfrastructureBuilder Build() => _parentBuilder;

    private void LogSetupInfo(string message)
    {
        _logger.LogDebug("[Integrations Setup] {Message}", message);
    }
}