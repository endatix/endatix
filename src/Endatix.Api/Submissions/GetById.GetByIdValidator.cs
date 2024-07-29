using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Submissions;

/// <summary>
/// Validation rules for the <c>GetByIdRequest</c> class.
/// </summary>
public class GetByIdValidator : Validator<GetByIdRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public GetByIdValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.SubmissionId)
            .GreaterThan(0);
    }
}
