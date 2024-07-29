using FluentValidation;
using Endatix.Core.Features.Email;

namespace Endatix.Core;

public class BaseEmailModelValidator : AbstractValidator<BaseEmailModel>
{
    public BaseEmailModelValidator()
    {
        RuleFor(x => x.From)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.To)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Subject)
           .NotEmpty();

        RuleFor(x => x.Metadata)
            .NotNull();
    }
}
