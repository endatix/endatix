using FastEndpoints;
using FluentValidation;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Validation rules for the <c>UpdateSubmissionRequest</c> class.
/// </summary>
public class UpdateSubmissionValidator : Validator<UpdateSubmissionRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public UpdateSubmissionValidator()
    {
        RuleFor(x => x.SubmissionId)
           .GreaterThan(0);

        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.JsonData)
            .NotEmpty()
            .MinimumLength(DataSchemaConstants.MIN_JSON_LENGTH);

        RuleFor(x => x.CurrentPage)
            .GreaterThan(0)
            .When(x => x.CurrentPage != null);
    }
}
