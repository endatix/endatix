using FluentValidation;

namespace Endatix.Core;

public class EmailWithTemplateValidator : AbstractValidator<EmailWithTemplate>
{
    public EmailWithTemplateValidator()
    {
        Include(new BaseEmailModelValidator());

        RuleFor(x => x.TemplateId)
            .NotEmpty();
    }
}
