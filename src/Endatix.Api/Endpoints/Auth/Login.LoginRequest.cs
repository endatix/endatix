namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Request model for initiating Login request
/// </summary>
public record LoginRequest(string Email, string Password);