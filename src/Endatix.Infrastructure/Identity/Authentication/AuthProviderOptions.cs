namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Base configuration options for authentication providers
/// </summary>
public abstract class AuthProviderOptions
{
    /// <summary>
    /// Whether this authentication provider is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The authentication scheme name for this provider
    /// </summary>
    public string SchemeName { get; set; } = string.Empty;

    /// <summary>
    /// Whether to require HTTPS metadata (defaults to true in production)
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Whether to map inbound claims from the provider
    /// </summary>
    public bool MapInboundClaims { get; set; } = false;

    /// <summary>
    /// The default tenant ID to use for the authentication provider
    /// </summary>
    public long DefaultTenantId { get; set; } = AuthConstants.DEFAULT_TENANT_ID;
}