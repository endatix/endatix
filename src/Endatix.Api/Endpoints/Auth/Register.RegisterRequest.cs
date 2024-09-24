namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// The request type for the "/register" endpoint added by <see cref="Register.HandleAsync"/> method.
/// </summary>
/// <param name="Email">The email address of the user.</param>
/// <param name="Password">The password chosen by the user.</param>
/// <param name="ConfirmPassword">The confirmation of the password chosen by the user.</param>
public record RegisterRequest(string Email, string Password, string ConfirmPassword);