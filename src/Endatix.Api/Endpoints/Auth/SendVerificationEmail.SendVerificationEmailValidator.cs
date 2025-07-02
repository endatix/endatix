using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Validator for the SendVerificationEmailRequest used in the email verification sending process.
/// </summary>
public class SendVerificationEmailValidator : Validator<SendVerificationEmailRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SendVerificationEmailValidator"/> class.
    /// </summary>
    public SendVerificationEmailValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
} 