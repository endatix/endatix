using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Validation rules for the <c>GetSubmissionFilesRequest</c> class.
/// </summary>
public class GetSubmissionFilesValidator : Validator<GetSubmissionFilesRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public GetSubmissionFilesValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.SubmissionId)
            .GreaterThan(0);

        RuleFor(x => x.FileNamesPrefix)
            .MaximumLength(100)
            .WithMessage("File names prefix must be less than 100 characters");
    }
}