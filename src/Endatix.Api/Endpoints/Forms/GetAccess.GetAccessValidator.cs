using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Forms;

public class GetAccessValidator : Validator<GetAccessRequest>
{
    public GetAccessValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.SubmissionId)
            .GreaterThan(0)
            .When(x => x.SubmissionId != null);

        RuleFor(x => x.Token)
            .NotEmpty()
            .When(x => x.Token != null);
    }
}
