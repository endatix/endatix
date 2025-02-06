using Endatix.Core.Entities;
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Validator for updating a form submission status.
/// </summary>
public class UpdateStatusValidator : Validator<UpdateStatusRequest>
{
    public UpdateStatusValidator()
    {
        RuleFor(x => x.SubmissionId)
            .GreaterThan(0);

        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.Status)
            .NotEmpty()
            .MaximumLength(SubmissionStatus.STATUS_CODE_MAX_LENGTH);
    }
}