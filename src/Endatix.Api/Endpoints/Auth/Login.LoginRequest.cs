namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Request model for initiating Login request
/// </summary>
public record LoginRequest
{
    /// <summary>
    /// Email of the user. Must be a valid email address
    /// </summary>
    public string Email { get; init; }

    /// <summary>
    /// Password of the account
    /// </summary>
    public string Password { get; init; }
}
