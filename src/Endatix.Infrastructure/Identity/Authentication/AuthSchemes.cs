using System.Collections.Concurrent;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Authentication scheme constants and dynamic scheme registry.
/// Supports both built-in schemes and runtime registration of custom schemes.
/// </summary>
public static class AuthSchemes
{
    /// <summary>
    /// Default Endatix JWT authentication scheme.
    /// This is the primary scheme for Endatix-generated tokens.
    /// </summary>
    public const string Endatix = "Endatix";

    /// <summary>
    /// Keycloak authentication scheme.
    /// Used for tokens from Keycloak identity providers.
    /// </summary>
    public const string Keycloak = "Keycloak";

    /// <summary>
    /// Multi-scheme policy that routes to appropriate schemes based on token content.
    /// This is typically set as the default authentication scheme.
    /// </summary>
    public const string MultiScheme = "MultiScheme";

    // Thread-safe storage for dynamically registered schemes
    private static readonly ConcurrentDictionary<string, string> _customSchemes = new();

    /// <summary>
    /// Registers a custom authentication scheme.
    /// Used by authentication providers to register their schemes dynamically.
    /// </summary>
    /// <param name="schemeName">The name of the authentication scheme</param>
    /// <param name="description">Optional description of the scheme</param>
    /// <exception cref="ArgumentException">Thrown when scheme name is invalid</exception>
    /// <exception cref="InvalidOperationException">Thrown when scheme is already registered</exception>
    public static void RegisterScheme(string schemeName, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemeName);

        if (IsBuiltInScheme(schemeName))
        {
            throw new InvalidOperationException($"Cannot register built-in scheme '{schemeName}'");
        }

        if (!_customSchemes.TryAdd(schemeName, description ?? schemeName))
        {
            throw new InvalidOperationException($"Authentication scheme '{schemeName}' is already registered");
        }
    }

    /// <summary>
    /// Checks if a scheme is a built-in Endatix scheme.
    /// </summary>
    /// <param name="schemeName">The scheme name to check</param>
    /// <returns>True if the scheme is built-in, false otherwise</returns>
    public static bool IsBuiltInScheme(string schemeName)
    {
        return schemeName is Endatix or Keycloak or MultiScheme;
    }

    /// <summary>
    /// Checks if a scheme is registered (either built-in or custom).
    /// </summary>
    /// <param name="schemeName">The scheme name to check</param>
    /// <returns>True if the scheme is registered, false otherwise</returns>
    public static bool IsRegistered(string schemeName)
    {
        return IsBuiltInScheme(schemeName) || _customSchemes.ContainsKey(schemeName);
    }

    /// <summary>
    /// Gets all registered scheme names.
    /// </summary>
    /// <returns>Collection of all registered scheme names</returns>
    public static IReadOnlyCollection<string> GetAllSchemes()
    {
        var allSchemes = new List<string> { Endatix, Keycloak, MultiScheme };
        allSchemes.AddRange(_customSchemes.Keys);
        return allSchemes.AsReadOnly();
    }
}