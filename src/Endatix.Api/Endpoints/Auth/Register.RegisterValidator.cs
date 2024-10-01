
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Validator for the RegisterRequest used in the user registration process.
/// </summary>
/// <remarks>
/// This validator ensures that:
/// - The email is not empty and is a valid email address.
/// - The password is not empty and has a minimum length of 8 characters.
/// - The confirm password matches the password.
/// </remarks>
public class RegisterValidator : Validator<RegisterRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterValidator"/> class.
    /// </summary>
    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.ConfirmPassword).NotEmpty().Equal(x => x.Password).WithMessage("Passwords do not match");
    }
}