namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// A response record used in the Login result
/// </summary>
/// <param name="Email">Email for the user initiating the successful login request</param>
/// <param name="Token">Authentication token returned upon success</param>
/// <param name="RefreshToken">Refresh token returned upon success</param>
public record LoginResponse(string Email, string Token, string RefreshToken);
