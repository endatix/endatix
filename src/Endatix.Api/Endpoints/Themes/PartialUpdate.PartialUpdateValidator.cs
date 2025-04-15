using Endatix.Api.Endpoints.Themes;
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Validation rules for the <c>PartialUpdateFormRequest</c> class.
/// </summary>
public class PartialUpdateValidator : Validator<PartialUpdateRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public PartialUpdateValidator()
    {
        RuleFor(x => x.ThemeId)
            .GreaterThan(0);
    }
}
