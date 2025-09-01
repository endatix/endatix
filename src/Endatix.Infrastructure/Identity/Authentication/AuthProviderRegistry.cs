using Ardalis.GuardClauses;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Registry for authentication providers.
/// </summary>
public class AuthProviderRegistry
{
    private readonly Dictionary<string, AuthProviderInfo> _providers = [];

    /// <summary>
    /// Registers an authentication provider.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type for the provider.</typeparam>
    /// <param name="provider">The authentication provider to register.</param>
    public void RegisterProvider<TConfig>(IAuthProvider provider) where TConfig : AuthProviderOptions
    {
        Guard.Against.Null(provider);
        Guard.Against.NullOrWhiteSpace(provider.SchemeName);
        Guard.Against.Null(typeof(TConfig), nameof(TConfig));

        if (_providers.ContainsKey(provider.SchemeName))
        {
            throw new InvalidOperationException($"Provider with scheme name {provider.SchemeName} already registered");
        }

        _providers[provider.SchemeName] = new AuthProviderInfo(provider, typeof(TConfig));
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
                .Values
                .FirstOrDefault(p => p.Provider.CanHandle(issuer, rawToken))?
                .Provider.SchemeName;
    }

    public AuthProviderInfo? GetProviderInfo(string schemeName) => _providers[schemeName];

    public IEnumerable<AuthProviderInfo> GetProviders() => _providers.Values;

    public sealed record AuthProviderInfo(IAuthProvider Provider, Type ConfigType);
}