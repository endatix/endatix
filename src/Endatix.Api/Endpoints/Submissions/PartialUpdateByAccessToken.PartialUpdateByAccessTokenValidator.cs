using Endatix.Infrastructure.Data.Config;
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Validation rules for the <c>PartialUpdateByAccessTokenRequest</c> class.
/// </summary>
public class PartialUpdateByAccessTokenValidator : Validator<PartialUpdateByAccessTokenRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public PartialUpdateByAccessTokenValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.Token)
            .NotEmpty();

        RuleFor(x => x.JsonData)
            .MinimumLength(DataSchemaConstants.MIN_JSON_LENGTH)
            .When(x => x.JsonData != null);

        RuleFor(x => x.CurrentPage)
            .GreaterThanOrEqualTo(0)
            .When(x => x.CurrentPage != null);
    }
}
