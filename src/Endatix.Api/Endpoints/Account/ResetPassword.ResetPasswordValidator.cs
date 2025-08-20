using Endatix.Api.Common.Security;
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Account;

public class ResetPasswordValidator : Validator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.ResetCode)
            .NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .SetValidator(new PasswordValidator(nameof(ResetPasswordRequest.NewPassword)));

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .WithMessage("Confirm your new password")
            .Equal(x => x.NewPassword)
            .WithMessage("Passwords do not match");
    }
}
