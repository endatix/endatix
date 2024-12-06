using FastEndpoints;
using FluentValidation;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Validation rules for the <c>PartialUpdateSubmissionByTokenRequest</c> class.
/// </summary>
public class PartialUpdateSubmissionByTokenValidator : Validator<PartialUpdateSubmissionByTokenRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public PartialUpdateSubmissionByTokenValidator()
    {
        RuleFor(x => x.SubmissionToken)
            .NotEmpty();

        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.JsonData)
            .MinimumLength(DataSchemaConstants.MIN_JSON_LENGTH)
            .When(x => x.JsonData != null);

        RuleFor(x => x.CurrentPage)
            .GreaterThanOrEqualTo(0)
            .When(x => x.CurrentPage != null);
    }
}