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
    /// <param name="rawToken">The JWT token string (without "Bearer " prefix)</param>
    /// <returns>The authentication scheme name, or default scheme if no match found</returns>
    string SelectScheme(string rawToken);

    /// <summary>
    /// Gets the default authentication scheme used when no specific p
    /// rovider is matched.
    /// </summary>
    string DefaultScheme { get; }
} 