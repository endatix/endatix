namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Request model for initiating Login request
/// </summary>
/// <param name="Email">Email of the user. Must be a valid email address</param>
/// <param name="Password">Password of the account</param>
public record LoginRequest(string Email, string Password);