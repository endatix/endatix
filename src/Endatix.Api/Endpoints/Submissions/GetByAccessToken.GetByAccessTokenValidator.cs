using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Validation rules for the <c>GetByAccessTokenRequest</c> class.
/// </summary>
public class GetByAccessTokenValidator : Validator<GetByAccessTokenRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public GetByAccessTokenValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.Token)
            .NotEmpty();
    }
}
