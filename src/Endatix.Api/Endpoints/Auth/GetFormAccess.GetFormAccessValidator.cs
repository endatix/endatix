using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Auth;

public class GetFormAccessValidator : Validator<GetFormAccessRequest>
{
    public GetFormAccessValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.Token)
            .NotEmpty()
            .When(x => x.Token != null);

        RuleFor(x => x.TokenType)
            .NotNull()
            .IsInEnum()
            .When(x => !string.IsNullOrEmpty(x.Token));
    }
}
