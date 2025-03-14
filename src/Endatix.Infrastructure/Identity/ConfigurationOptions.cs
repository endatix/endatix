namespace Endatix.Infrastructure.Identity;

/// <summary>
/// Configuration options for setting up the Endatix infrastructure.
/// </summary>
public class ConfigurationOptions
{
    /// <summary>
    /// Gets or sets the JWT settings.
    /// </summary>
    public JwtSettings? JwtSettings { get; set; }
    
    /// <summary>
    /// Gets or sets the initial user settings.
    /// </summary>
    public InitialUserSettings? InitialUserSettings { get; set; }
}

/// <summary>
/// JWT configuration settings.
/// </summary>
public class JwtSettings
{
    /// <summary>
    /// Gets or sets the secret key used for signing tokens.
    /// </summary>
    public string Secret { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the token issuer.
    /// </summary>
    public string Issuer { get; set; } = "Endatix";
    
    /// <summary>
    /// Gets or sets the token audience.
    /// </summary>
    public string Audience { get; set; } = "EndatixApi";
    
    /// <summary>
    /// Gets or sets the token expiration time in minutes.
    /// </summary>
    public int ExpirationInMinutes { get; set; } = 60;
}

/// <summary>
/// Settings for the initial admin user creation.
/// </summary>
public class InitialUserSettings
{
    /// <summary>
    /// Gets or sets the email address for the initial user.
    /// </summary>
    public string Email { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the password for the initial user.
    /// </summary>
    public string Password { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the role for the initial user.
    /// </summary>
    public string Role { get; set; } = "Admin";
} 