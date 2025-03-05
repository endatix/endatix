using Endatix.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Builders;

/// <summary>
/// Builder for configuring Endatix identity infrastructure.
/// </summary>
public class InfrastructureIdentityBuilder
{
    private readonly InfrastructureBuilder _parentBuilder;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the InfrastructureIdentityBuilder class.
    /// </summary>
    /// <param name="parentBuilder">The parent infrastructure builder.</param>
    internal InfrastructureIdentityBuilder(InfrastructureBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
        _logger = parentBuilder.Services.BuildServiceProvider().GetService<ILoggerFactory>()?.CreateLogger("Endatix.Setup");
    }

    /// <summary>
    /// Configures identity infrastructure with default settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public InfrastructureIdentityBuilder UseDefaults()
    {
        LogSetupInfo("Configuring identity with default settings");

        // Configure identity services with default settings
        _parentBuilder.Services.AddIdentityConfiguration();

        LogSetupInfo("Identity configuration completed");
        return this;
    }

    /// <summary>
    /// Configures identity with custom settings.
    /// </summary>
    /// <param name="options">The identity configuration options.</param>
    /// <returns>The builder for chaining.</returns>
    public InfrastructureIdentityBuilder Configure(Identity.ConfigurationOptions options)
    {
        LogSetupInfo("Configuring identity with custom settings");

        // Add identity services with custom options
        _parentBuilder.Services.AddIdentityConfiguration(options);

        LogSetupInfo("Identity configuration completed");
        return this;
    }

    /// <summary>
    /// Returns to the parent infrastructure builder.
    /// </summary>
    /// <returns>The parent infrastructure builder.</returns>
    public InfrastructureBuilder Parent() => _parentBuilder;

    private void LogSetupInfo(string message)
    {
        _logger?.LogInformation("[Identity Setup] {Message}", message);
    }
}