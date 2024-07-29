using System.ComponentModel.DataAnnotations;

namespace Endatix.Api;

/// <summary>
/// Request model for initiating Login request
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// Email of the user. Must be a valid email address
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Password of the account
    /// </summary>
    public string Password { get; set; }
}
