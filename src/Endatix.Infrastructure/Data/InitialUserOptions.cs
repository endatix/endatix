namespace Endatix.Infrastructure.Identity;

/// <summary>
/// Configuration options for the initial user account.
/// </summary>
public class InitialUserOptions
{
    /// <summary>
    /// The email address for the initial user account.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The password for the initial user account.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
