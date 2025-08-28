using Ardalis.GuardClauses;

namespace Endatix.Infrastructure.Identity.Authentication;


public class AuthProviderRegistry
{
    private readonly Dictionary<string, AuthProviderInfo> _providers = [];

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

    public AuthProviderInfo? GetProviderInfo(string schemeName) => _providers[schemeName];

    public IEnumerable<AuthProviderInfo> GetProviders() => _providers.Values;

    public sealed record AuthProviderInfo(IAuthProvider Provider, Type ConfigType);
}