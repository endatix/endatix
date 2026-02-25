using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.AccessControl;

public class GetPermissionsValidator : Validator<GetPermissionsRequest>
{
    public GetPermissionsValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.SubmissionId)
            .GreaterThan(0)
            .When(x => x.SubmissionId != null);
    }
}
