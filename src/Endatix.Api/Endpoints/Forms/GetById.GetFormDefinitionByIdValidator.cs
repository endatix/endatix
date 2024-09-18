using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Validation rules for the <c>GetFormByIdRequest</c> class.
/// </summary>
public class GetFormByIdValidator : Validator<GetFormByIdRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public GetFormByIdValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);
    }
}
