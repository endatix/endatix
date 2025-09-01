using System.Text.Json;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Default implementation of auth scheme selection based on JWT token issuers.
/// Designed to be replaced/extended by a plugin-based system in the future.
/// </summary>
internal sealed class DefaultAuthSchemeSelector : IAuthSchemeSelector
{
    /// <summary>
    /// The default authentication scheme used when no specific provider matches.
    /// </summary>
    public string DefaultScheme => AuthSchemes.EndatixJwt;

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
            return DefaultScheme;
        }

        // TODO: Replace with plugin-based provider registry in the future
        // This will become: return _providerRegistry.SelectScheme(issuer) ?? DefaultScheme;
        return SelectSchemeByIssuer(issuer);
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

    /// <summary>
    /// Selects authentication scheme based on issuer patterns.
    /// This method will be replaced by a plugin-based provider registry.
    /// </summary>
    /// <param name="issuer">The JWT issuer claim</param>
    /// <returns>The authentication scheme name</returns>
    private string SelectSchemeByIssuer(string issuer)
    {
        // Current hardcoded routing logic - future: move to provider registry
        return issuer switch
        {
            // Exact match for Endatix tokens
            "endatix-api" => AuthSchemes.EndatixJwt,
            
            // Pattern match for Keycloak (supports different realms)
            string iss when iss.Contains("localhost:8080/realms/endatix") => AuthSchemes.Keycloak,
            
            string iss when iss.Contains("accounts.google.com") => "Google",
            // Future providers can be added here or via plugin system:
            // string iss when iss.Contains("auth0.com") => AuthSchemes.Auth0,
            // string iss when iss.Contains("login.microsoftonline.com") => AuthSchemes.AzureAD,
            
            // Default fallback
            _ => DefaultScheme
        };
    }
} 