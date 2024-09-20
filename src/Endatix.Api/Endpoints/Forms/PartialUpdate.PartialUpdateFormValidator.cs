using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Validation rules for the <c>PartialUpdateFormRequest</c> class.
/// </summary>
public class PartialUpdateFormValidator : Validator<PartialUpdateFormRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public PartialUpdateFormValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);
    }
}
