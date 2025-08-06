using System.ComponentModel.DataAnnotations;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Configuration options for an individual authentication provider.
/// Supports provider-specific configuration through the Config dictionary.
/// </summary>
public class AuthProviderOptions
{
    /// <summary>
    /// Unique identifier for the authentication provider.
    /// Must match the ProviderId of the corresponding IAuthenticationProvider implementation.
    /// </summary>
    [Required]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The type of authentication provider (e.g., "jwt", "keycloak", "auth0").
    /// Used to map to the appropriate IAuthenticationProvider implementation.
    /// </summary>
    [Required]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Whether this provider is enabled.
    /// Disabled providers will not be registered in the authentication pipeline.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Priority for provider selection when multiple providers can handle the same issuer.
    /// Lower values indicate higher priority (0 = highest priority).
    /// If not specified, defaults to the provider's default priority.
    /// </summary>
    public int? Priority { get; set; }

    /// <summary>
    /// Provider-specific configuration as key-value pairs.
    /// These will be mapped to strongly-typed options classes for each provider.
    /// </summary>
    public Dictionary<string, object> Config { get; set; } = new();
} 