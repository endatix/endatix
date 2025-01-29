using FastEndpoints;
using FluentValidation;
using Endatix.Api.Common.Security;

namespace Endatix.Api.Endpoints.MyAccount;

public class ChangePasswordValidator : Validator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("Current password is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("New password is required")
            .SetValidator(new PasswordValidator(nameof(ChangePasswordRequest.NewPassword)));

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .WithMessage("Password confirmation is required")
            .Equal(x => x.NewPassword)
            .WithMessage("Passwords do not match");
    }
}
