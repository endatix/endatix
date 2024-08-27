using System.Data;
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Validation rules for the <c>FormDefinitionsListRequest</c> class.
/// </summary>
public class FormDefinitionsListValidator : Validator<FormDefinitionsListRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public FormDefinitionsListValidator()
    {
        Include(new PageRequestValidator());

        RuleFor(x => x.FormId)
            .GreaterThan(0);
    }
}
