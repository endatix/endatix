using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Submissions;

public class ListByFormIdValidator : Validator<ListByFormIdRequest>
{
    public ListByFormIdValidator()
    {
        Include(new PageRequestValidator());

        RuleFor(x => x.FormId)
            .GreaterThan(0);
    }
}
