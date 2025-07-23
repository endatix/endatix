using FluentValidation;
using Endatix.Core.Features.Email;

namespace Endatix.Core;

public class EmailWithBodyValidator : AbstractValidator<EmailWithBody>
{
    public EmailWithBodyValidator()
    {
        Include(new BaseEmailModelValidator());

        RuleFor(x => x.From)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Subject)
           .NotEmpty();

        RuleFor(x => x.HtmlBody)
            .NotEmpty();

        RuleFor(x => x.PlainTextBody)
            .NotEmpty();
    }
}
