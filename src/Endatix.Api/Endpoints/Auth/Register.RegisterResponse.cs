namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// The response type for the "/register" endpoint returned by the <see cref="Register.HandleAsync"/> method.
/// </summary>
public record RegisterResponse(bool Success = false, string Message = "");