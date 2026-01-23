using FastEndpoints;
using FluentValidation;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Validation rules for the <c>CreateSubmissionOnBehalfRequest</c> class.
/// </summary>
public class CreateSubmissionOnBehalfValidator : Validator<CreateSubmissionOnBehalfRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public CreateSubmissionOnBehalfValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.JsonData)
            .MinimumLength(DataSchemaConstants.MIN_JSON_LENGTH)
            .When(x => x.JsonData != null);

        RuleFor(x => x.Metadata)
            .MinimumLength(DataSchemaConstants.MIN_JSON_LENGTH)
            .When(x => x.Metadata != null);

        RuleFor(x => x.SubmittedBy)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .When(x => x.SubmittedBy != null);
    }
}
