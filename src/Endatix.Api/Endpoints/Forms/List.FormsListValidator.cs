using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Validation rules for the <c>FormsListRequest</c> class.
/// </summary>
public class FormsListValidator : Validator<FormsListRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public FormsListValidator()
    {
        Include(new PageRequestValidator());
    }
}
