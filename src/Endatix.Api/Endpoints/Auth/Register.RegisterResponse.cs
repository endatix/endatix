namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// The response type for the "/register" endpoint returned by the <see cref="Register.HandleAsync"/> method.
/// </summary>
/// <param name="Success">Indicates whether the registration was successful.</param>
/// <param name="Message">A message describing the outcome of the registration attempt.</param>
public record RegisterResponse(bool Success, string Message);