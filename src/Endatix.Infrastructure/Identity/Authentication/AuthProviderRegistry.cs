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
public class AuthProviderRegistry
{
    private readonly List<ProviderRegistration> _providers = new();

    /// <summary>
    /// Register a provider with its configuration type and configure DI
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


        if (IsProviderRegistered(provider.SchemeName))
        {
            throw new InvalidOperationException($"Provider with scheme name {provider.SchemeName} already registered");
        }

        var registration = new ProviderRegistration(provider, configType, configPath);
        _providers.Add(registration);
    }


    /// <summary>
    /// Selects an authentication scheme based on the issuer and raw token.
    /// </summary>
    /// <param name="issuer">The issuer of the token.</param>
    /// <param name="rawToken">The raw token string (without "Bearer " prefix).</param>
    /// <returns>The authentication scheme name, or null if no provider can handle the token.</returns>
    public string? SelectScheme(string issuer, string rawToken)
    {
        return _providers
                .FirstOrDefault(registration => registration.Provider.CanHandle(issuer, rawToken))
                ?.Provider.SchemeName;
    }

    /// <summary>
    /// Check if a provider is registered
    /// </summary>
    public bool IsProviderRegistered(string schemeName) =>
        _providers.Any(reg => reg.Provider.SchemeName == schemeName);

    /// <summary>
    /// Get all registered provider registrations
    /// </summary>
    public IEnumerable<ProviderRegistration> GetProviderRegistrations() => _providers.AsReadOnly();
}