namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Provides fast JWT token issuer extraction without signature validation.
/// Used for routing tokens to appropriate authentication schemes based on issuer.
/// </summary>
public interface IJwtTokenInspector
{
    /// <summary>
    /// Extracts the issuer from a JWT token without validating the signature.
    /// </summary>
    /// <param name="token">The JWT token string (without "Bearer " prefix)</param>
    /// <returns>The issuer claim value, or null if not found or token is invalid</returns>
    string? GetIssuer(string token);
} 