namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// A response record used in the Login result
/// </summary>
public record LoginResponse(string Email, string Token, string RefreshToken);
