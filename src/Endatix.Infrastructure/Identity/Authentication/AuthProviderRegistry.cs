using System.Collections.Concurrent;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Default implementation of the authentication provider registry.
/// Manages provider registration and provides thread-safe scheme selection.
/// </summary>
internal sealed class AuthProviderRegistry : IAuthProviderRegistry
{
    private readonly ConcurrentDictionary<string, IAuthenticationProvider> _providers = new();

    /// <inheritdoc />
    public string DefaultScheme => AuthSchemes.Endatix;

    /// <inheritdoc />
    public void RegisterProvider(IAuthenticationProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        
        if (string.IsNullOrWhiteSpace(provider.ProviderId))
        {
            throw new ArgumentException("Provider ID cannot be null or whitespace", nameof(provider));
        }

        if (string.IsNullOrWhiteSpace(provider.Scheme))
        {
            throw new ArgumentException("Provider scheme cannot be null or whitespace", nameof(provider));
        }

        if (_providers.ContainsKey(provider.ProviderId))
        {
            throw new InvalidOperationException($"Provider with ID '{provider.ProviderId}' is already registered");
        }

        _providers.TryAdd(provider.ProviderId, provider);
    }

    /// <inheritdoc />
    public string SelectScheme(string issuer)
    {
        if (string.IsNullOrWhiteSpace(issuer))
        {
            return DefaultScheme;
        }

        // Find providers that can handle this issuer, ordered by priority
        var matchingProvider = _providers.Values
            .Where(p => p.CanHandleIssuer(issuer))
            .OrderBy(p => p.Priority)
            .ThenBy(p => p.ProviderId) // Stable sort for consistent behavior
            .FirstOrDefault();

        return matchingProvider?.Scheme ?? DefaultScheme;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IAuthenticationProvider> GetProviders()
    {
        return _providers.Values
            .OrderBy(p => p.Priority)
            .ThenBy(p => p.ProviderId)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public IAuthenticationProvider? GetProvider(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        
        return _providers.TryGetValue(providerId, out var provider) ? provider : null;
    }
} 