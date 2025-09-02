using System;
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Account;

public class ForgotPasswordValidator : Validator<ForgotPasswordRequest>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}
