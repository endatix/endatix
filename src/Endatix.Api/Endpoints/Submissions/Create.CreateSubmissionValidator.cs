using FastEndpoints;
using FluentValidation;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Api.Endpoints.Submissions;


/// <summary>
/// Validation rules for the <c>CreateSubmissionRequest</c> class.
/// </summary>
public class CreateSubmissionValidator : Validator<CreateSubmissionRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public CreateSubmissionValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.JsonData)
            .NotEmpty()
            .MinimumLength(DataSchemaConstants.MIN_JSON_LENGTH);
    }
}
