namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// The request type for the "/register" endpoint added by <see cref="Register.HandleAsync"/> method.
/// </summary>
public record RegisterRequest(string Email, string Password, string ConfirmPassword);