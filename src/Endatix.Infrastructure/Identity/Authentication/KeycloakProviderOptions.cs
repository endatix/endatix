using System.ComponentModel.DataAnnotations;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Strongly-typed configuration options for the Keycloak authentication provider.
/// Maps from the generic AuthProviderOptions.Config dictionary for type safety.
/// </summary>
public class KeycloakProviderOptions
{
    /// <summary>
    /// The Keycloak server metadata address for OpenID Connect configuration.
    /// Example: "https://auth.example.com/realms/myrealm/.well-known/openid-configuration"
    /// </summary>
    [Required]
    public string MetadataAddress { get; set; } = string.Empty;

    /// <summary>
    /// The expected issuer for tokens from this Keycloak instance.
    /// Should match the issuer claim in JWT tokens from Keycloak.
    /// Example: "https://auth.example.com/realms/myrealm"
    /// </summary>
    public string? ValidIssuer { get; set; }

    /// <summary>
    /// The expected audience for tokens (client ID in Keycloak).
    /// Example: "account", "my-client-id"
    /// </summary>
    public string? Audience { get; set; } = "account";

    /// <summary>
    /// Whether to require HTTPS for metadata requests.
    /// Should be false only in development environments.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Whether to validate the token issuer.
    /// Set to false for development or if using multiple Keycloak realms.
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;

    /// <summary>
    /// Whether to validate the token audience.
    /// Set to false if not using audience validation in Keycloak.
    /// </summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>
    /// Whether to validate token lifetime (expiration).
    /// Should generally be true for security.
    /// </summary>
    public bool ValidateLifetime { get; set; } = true;

    /// <summary>
    /// Whether to validate the issuer signing key.
    /// Should generally be true for security.
    /// </summary>
    public bool ValidateIssuerSigningKey { get; set; } = true;

    /// <summary>
    /// Whether to map inbound claims from Keycloak format.
    /// Set to true to use Keycloak's claim names and structure.
    /// </summary>
    public bool MapInboundClaims { get; set; } = true;

    /// <summary>
    /// Additional issuer patterns that this provider should handle.
    /// Used for matching tokens from different Keycloak realms or instances.
    /// </summary>
    public List<string> IssuerPatterns { get; set; } = new();
} 