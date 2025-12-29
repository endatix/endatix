using Endatix.Core.Infrastructure.Logging;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Represents the request for the "/register" endpoint, handled by the <see cref="Register.HandleAsync"/> method.
/// </summary>
public record RegisterRequest(string Email, string Password, string ConfirmPassword)
{
    /// <summary>
    /// The email address of the user.
    /// </summary>
    public string Email { get; init; } = Email;

    /// <summary>
    /// The password chosen by the user.
    /// </summary>
    [Sensitive]
    public string Password { get; init; } = Password;

    /// <summary>
    /// The confirmation of the password chosen by the user.
    /// </summary>
    [Sensitive]
    public string ConfirmPassword { get; init; } = ConfirmPassword;
}
