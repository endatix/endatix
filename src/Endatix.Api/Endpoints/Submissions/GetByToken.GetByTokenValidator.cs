using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Validation rules for the <c>GetByTokenRequest</c> class.
/// </summary>
public class GetByTokenValidator : Validator<GetByTokenRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public GetByTokenValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.SubmissionToken)
            .NotEmpty();
    }
}
