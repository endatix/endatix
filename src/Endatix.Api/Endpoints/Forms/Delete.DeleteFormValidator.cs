using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Validation rules for the <c>DeleteFormRequest</c> class.
/// </summary>
public class DeleteFormValidator : Validator<DeleteFormRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public DeleteFormValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);
    }
}
