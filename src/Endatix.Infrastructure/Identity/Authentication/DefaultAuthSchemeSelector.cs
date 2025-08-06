using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Default implementation of auth scheme selection that delegates to a provider registry.
/// Uses the provider registry to select appropriate schemes based on token issuers.
/// </summary>
internal sealed class DefaultAuthSchemeSelector : IAuthSchemeSelector
{
    private readonly IAuthProviderRegistry _providerRegistry;
    private readonly ILogger<DefaultAuthSchemeSelector>? _logger;

    /// <summary>
    /// Initializes a new instance of the DefaultAuthSchemeSelector.
    /// </summary>
    /// <param name="providerRegistry">The provider registry for scheme selection</param>
    /// <param name="logger">Optional logger for debugging</param>
    public DefaultAuthSchemeSelector(IAuthProviderRegistry providerRegistry, ILogger<DefaultAuthSchemeSelector>? logger = null)
    {
        _providerRegistry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
        _logger = logger;
    }

    /// <summary>
    /// The default authentication scheme used when no specific provider matches.
    /// </summary>
    public string DefaultScheme => _providerRegistry.DefaultScheme;

    /// <inheritdoc />
    public string SelectScheme(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return DefaultScheme;
        }

        var issuer = ExtractIssuerFromToken(token);
        if (string.IsNullOrEmpty(issuer))
        {
            _logger?.LogDebug("No issuer found in token, using default scheme: {DefaultScheme}", DefaultScheme);
            return DefaultScheme;
        }

        var selectedScheme = _providerRegistry.SelectScheme(issuer);
        _logger?.LogDebug("Selected scheme {Scheme} for issuer {Issuer}", selectedScheme, issuer);
        
        return selectedScheme;
    }

    /// <summary>
    /// Extracts the issuer from a JWT token without signature validation.
    /// Optimized for performance in auth scheme routing scenarios.
    /// </summary>
    /// <param name="token">The JWT token string (without "Bearer " prefix)</param>
    /// <returns>The issuer claim value, or null if not found or token is invalid</returns>
    private static string? ExtractIssuerFromToken(string token)
    {
        try
        {
            using var doc = ParseJwtPayload(token);
            return doc?.RootElement.TryGetProperty("iss", out var iss) == true 
                ? iss.GetString() 
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses JWT payload without signature validation for performance.
    /// </summary>
    /// <param name="token">The JWT token string</param>
    /// <returns>JsonDocument of the payload, or null if parsing fails</returns>
    private static JsonDocument? ParseJwtPayload(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        var payload = parts[1];

        // Add Base64URL padding if missing
        var padding = 4 - payload.Length % 4;
        if (padding < 4)
        {
            payload += new string('=', padding);
        }

        try
        {
            var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
            return JsonDocument.Parse(bytes);
        }
        catch
        {
            return null;
        }
    }


} 