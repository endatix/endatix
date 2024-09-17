using FastEndpoints;
using FluentValidation;
using FluentValidation.Validators;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Validation rules for the <c>LoginRequest</c> class.
/// </summary>
public class LoginRequestValidator : Validator<LoginRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("an email is required!")
            .EmailAddress(EmailValidationMode.AspNetCoreCompatible).WithMessage("Email must be a valid email address format");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("a password is required!")
            .MinimumLength(8).WithMessage("password is too short!");
    }
}
