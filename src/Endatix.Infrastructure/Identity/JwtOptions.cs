using System.ComponentModel.DataAnnotations;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// Configuration options for JWT (JSON Web Token) Bearer token used for authentication and authorization
/// </summary>
[Obsolete("Use EndatixJwtOptions instead. Will be removed in the future.")]
public class JwtOptions
{
    /// <summary>
    /// The configuration section name where these options are stored.
    /// </summary>
    public const string SECTION_NAME = "Endatix:Jwt";

    /// <summary>
    /// The key used to sign the JWT token.
    /// This is required and must be set in the configuration.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// The expiration time of the JWT access token in minutes.
    /// Default value: 15 minutes
    /// </summary>
    public int AccessExpiryInMinutes { get; set; } = 15;

    /// <summary>
    /// The expiration time of the JWT refresh token in minutes.
    /// Default value: 7 days
    /// </summary>
    public int RefreshExpiryInDays { get; set; } = 7;

    /// <summary>
    /// Valid issuer for the JWT token.
    /// Example: "api.myapp.com" or "https://localhost:5000" or "endatix-api"
    /// </summary>
    public string? Issuer { get; set; } = "endatix-api";

    /// <summary>
    /// List of valid audiences for the JWT token.
    /// Example: ["www.myapp.com", "https://localhost:3000", "my-endatix-app"]
    /// Default value: Empty list
    /// </summary>
    public IList<string> Audiences { get; set; } = ["endatix-hub"];
}
