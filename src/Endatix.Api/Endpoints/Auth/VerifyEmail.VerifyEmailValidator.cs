using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Validator for the VerifyEmailRequest used in the email verification process.
/// </summary>
public class VerifyEmailValidator : Validator<VerifyEmailRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VerifyEmailValidator"/> class.
    /// </summary>
    public VerifyEmailValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty();
    }
}