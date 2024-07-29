using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Submissions;

public class ListByFormIdValidator : Validator<ListByFormIdRequest>
{
    public ListByFormIdValidator()
    {
        Include(new PageRequestValidator());

        RuleFor(x => x.FormId)
            .GreaterThan(0);
    }
}
