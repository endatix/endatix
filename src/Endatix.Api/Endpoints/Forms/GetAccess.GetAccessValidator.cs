using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Forms;

public class GetAccessValidator : Validator<GetAccessRequest>
{
    public GetAccessValidator()
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
