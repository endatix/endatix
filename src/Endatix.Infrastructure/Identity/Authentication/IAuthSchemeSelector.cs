namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Selects the appropriate authentication scheme for a JWT token.
/// Designed to support pluggable auth providers and custom routing logic.
/// </summary>
public interface IAuthSchemeSelector
{
    /// <summary>
    /// Selects the appropriate authentication scheme for the given JWT token.
    /// </summary>
    /// <param name="token">The JWT token string (without "Bearer " prefix)</param>
    /// <returns>The authentication scheme name, or default scheme if no match found</returns>
    string SelectScheme(string token);

    /// <summary>
    /// Gets the default authentication scheme used when no specific provider is matched.
    /// </summary>
    string DefaultScheme { get; }
} 