namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// A response record used in the Login result
/// </summary>
public record LoginResponse(string Email, string Token, string RefreshToken)
{
    /// <summary>
    /// Email for the user initiating the successful login request
    /// </summary>
    public string Email { get; init; } = Email;

    /// <summary>
    /// Authentication token returned upon success
    /// </summary>
    public string Token { get; init; } = Token;

    /// <summary>
    /// Refresh token returned upon success
    /// </summary>
    public string RefreshToken { get; init; } = RefreshToken;
}
