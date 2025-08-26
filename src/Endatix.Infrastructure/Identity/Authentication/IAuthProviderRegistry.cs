namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Registry for managing authentication providers and selecting appropriate schemes.
/// Replaces hardcoded issuer-to-scheme mapping with a pluggable provider system.
/// </summary>
public interface IAuthProviderRegistry
{
    /// <summary>
    /// Registers an authentication provider in the registry.
    /// </summary>
    /// <param name="provider">The authentication provider to register</param>
    void RegisterProvider(IAuthenticationProvider provider);

    /// <summary>
    /// Selects the appropriate authentication scheme for the given issuer.
    /// Uses provider priority and issuer matching logic to determine the best scheme.
    /// </summary>
    /// <param name="issuer">The JWT issuer claim to match against providers</param>
    /// <returns>The authentication scheme name, or default scheme if no provider matches</returns>
    string SelectScheme(string issuer);

    /// <summary>
    /// Gets all registered providers, ordered by priority.
    /// </summary>
    /// <returns>Collection of registered providers</returns>
    IReadOnlyCollection<IAuthenticationProvider> GetProviders();

    /// <summary>
    /// Gets a provider by its ID.
    /// </summary>
    /// <param name="providerId">The unique provider identifier</param>
    /// <returns>The provider if found, null otherwise</returns>
    IAuthenticationProvider? GetProvider(string providerId);

    /// <summary>
    /// Gets the default authentication scheme used when no provider matches.
    /// </summary>
    string DefaultScheme { get; }
} 