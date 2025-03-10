using Endatix.Framework.Configuration;

namespace Endatix.Infrastructure.Setup;

/// <summary>
/// Configuration options for security features.
/// </summary>
public class SecurityOptions : EndatixOptionsBase
{
    /// <summary>
    /// Gets the section path for these options.
    /// </summary>
    public override string SectionPath => "Security";

    // Add your security properties here
    /// <summary>
    /// Gets or sets the JWT secret key.
    /// </summary>
    public string JwtSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JWT token expiration in minutes.
    /// </summary>
    public int JwtExpirationInMinutes { get; set; } = 60;
} 