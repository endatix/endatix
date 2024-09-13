namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// A response record used in the Login result
/// </summary>
public record LoginResponse
{
    /// <summary>
    /// Email for the user initiating the successful login request
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// Authentication token returned upon success
    /// </summary>
    public required string Token { get; set; }
}
