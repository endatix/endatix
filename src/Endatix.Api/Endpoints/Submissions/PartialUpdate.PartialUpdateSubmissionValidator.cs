using FastEndpoints;
using FluentValidation;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Validation rules for the <c>PartialUpdateSubmissionRequest</c> class.
/// </summary>
public class PartialUpdateSubmissionValidator : Validator<PartialUpdateSubmissionRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public PartialUpdateSubmissionValidator()
    {
        RuleFor(x => x.SubmissionId)
           .GreaterThan(0);

        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.JsonData)
            .MinimumLength(DataSchemaConstants.MIN_JSON_LENGTH)
            .When(x => x.JsonData != null);

        RuleFor(x => x.CurrentPage)
            .GreaterThan(0)
            .When(x => x.CurrentPage != null);
    }
}