using System.ComponentModel.DataAnnotations;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Configuration options for JWT-based authentication providers
/// </summary>
public abstract class JwtAuthProviderOptions : AuthProviderOptions
{
    /// <summary>
    /// Valid issuer for the JWT token
    /// </summary>
    [Required]
    public virtual string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Whether to validate the issuer
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;

    /// <summary>
    /// Valid audiences for the JWT token
    /// </summary>
    public IList<string> Audiences { get; set; } = [];

    /// <summary>
    /// Whether to validate the audience
    /// </summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>
    /// Whether to validate the token lifetime
    /// </summary>
    public bool ValidateLifetime { get; set; } = true;

    /// <summary>
    /// Whether to validate the issuer signing key
    /// </summary>
    public bool ValidateIssuerSigningKey { get; set; } = true;

    /// <summary>
    /// Clock skew tolerance in seconds
    /// </summary>
    public int ClockSkewSeconds { get; set; } = 15;
}