using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Validation rules for the <c>DeleteSubmissionRequest</c> class.
/// </summary>
public class DeleteSubmissionValidator : Validator<DeleteSubmissionRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public DeleteSubmissionValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.SubmissionId)
            .GreaterThan(0);
    }
}