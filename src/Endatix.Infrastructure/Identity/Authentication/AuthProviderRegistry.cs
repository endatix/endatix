using Ardalis.GuardClauses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Provider record containing provider instance and configuration type information
/// </summary>
public record ProviderRegistration(IAuthProvider Provider, Type ConfigType, string ConfigurationSectionPath);


/// <summary>
/// Registry for authentication providers.
/// </summary>
public sealed class AuthProviderRegistry
{
    private readonly List<ProviderRegistration> _requestedRegistrations = new();
    private readonly List<IAuthProvider> _activeProviders = new();

    /// <summary>
    /// Add a provider to the active providers list after successful configuration
    /// </summary>
    internal void AddActiveProvider(IAuthProvider provider)
    {
        Guard.Against.Null(provider);
        Guard.Against.NullOrWhiteSpace(provider.SchemeName);


        if (!IsProviderRegistrationRequested(provider.SchemeName))
        {
            throw new InvalidOperationException(
                $"Provider with scheme name {provider.SchemeName} was not requested for registration. " +
                $"Only providers that have been registered via RegisterProvider() can be activated.");
        }

        if (IsProviderActive(provider.SchemeName))
        {
            throw new InvalidOperationException($"Provider with scheme name {provider.SchemeName} already active & configured in the system");
        }

        _activeProviders.Add(provider);
    }

    /// <summary>
    /// Register a provider with its configuration type and configure DI if configuration is valid and enabled
    /// </summary>
    /// <typeparam name="TConfig">The configuration type for the provider.</typeparam>
    /// <param name="provider">The authentication provider to register.</param>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration object.</param>
    public void RegisterProvider<TConfig>(IAuthProvider provider, IServiceCollection services, IConfiguration configuration)
       where TConfig : AuthProviderOptions, new()
    {
        Guard.Against.Null(provider);
        Guard.Against.Null(services);
        Guard.Against.Null(configuration);
        Guard.Against.NullOrWhiteSpace(provider.SchemeName);
        Guard.Against.Null(typeof(TConfig), nameof(TConfig));

        var configType = typeof(TConfig);
        var configPath = provider.ConfigurationSectionPath;

        // Register the provider configuration in DI
        var providerConfigSection = configuration.GetSection(configPath);
        services.AddOptions<TConfig>()
                .BindConfiguration(configPath)
                .ValidateDataAnnotations()
                .ValidateOnStart();


        if (IsProviderRegistrationRequested(provider.SchemeName))
        {
            throw new InvalidOperationException($"Provider with scheme name {provider.SchemeName} already requested for registration");
        }

        var registration = new ProviderRegistration(provider, configType, configPath);
        _requestedRegistrations.Add(registration);
    }


    /// <summary>
    /// Selects an authentication scheme based on the issuer and raw token.
    /// Only considers active (configured) providers.
    /// </summary>
    /// <param name="issuer">The issuer of the token.</param>
    /// <param name="rawToken">The raw token string (without "Bearer " prefix).</param>
    /// <returns>The authentication scheme name, or null if no provider can handle the token.</returns>
    public string? SelectScheme(string issuer, string rawToken)
    {
        return _activeProviders
                .FirstOrDefault(provider => provider.CanHandle(issuer, rawToken))
                ?.SchemeName;
    }

    /// <summary>
    /// Check if a provider registration is requested
    /// </summary>
    public bool IsProviderRegistrationRequested(string schemeName) =>
        _requestedRegistrations.Any(reg => reg.Provider.SchemeName == schemeName);

    /// <summary>
    /// Check if a provider is active in the system
    /// </summary>
    public bool IsProviderActive(string schemeName) =>
        _activeProviders.Any(provider => provider.SchemeName == schemeName);

    /// <summary>
    /// Get all requested provider registrations
    /// </summary>
    public IEnumerable<ProviderRegistration> GetRequestedRegistrations() => _requestedRegistrations.AsReadOnly();

    /// <summary>
    /// Get all active providers (Auth providers that are configured and enabled)
    /// </summary>
    public IEnumerable<IAuthProvider> GetActiveProviders() => _activeProviders.AsReadOnly();
}