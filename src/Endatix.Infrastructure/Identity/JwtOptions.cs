using System.ComponentModel.DataAnnotations;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// Configuration options for JWT (JSON Web Token) Bearer token used for authentication and authorization
/// </summary>
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
    [Required]
    public string? SigningKey { get; set; }

    /// <summary>
    /// The expiration time of the JWT token in minutes.
    /// Default value: 60 minutes (1 hour)
    /// </summary>
    public int ExpiryInMinutes { get; set; } = 60;

    /// <summary>
    /// Valid issuer for the JWT token.
    /// Example: "api.myapp.com" or "https://localhost:5000" or "endatix-api"
    /// </summary>
    public string? Issuer { get; set; } = "endatix-api";

    /// <summary>
    /// List of valid audiences for the JWT token.
    /// Example: ["www.myapp.com", "https://localhost:3000", "endatix-app"]
    /// Default value: Empty list
    /// </summary>
    public IList<string> Audiences { get; set; } = ["endatix-app"];
}
