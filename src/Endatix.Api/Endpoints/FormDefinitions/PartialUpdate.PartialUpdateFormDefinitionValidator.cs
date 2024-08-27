using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Validation rules for the <c>PartialUpdateFormDefinitionRequest</c> class.
/// </summary>
public class PartialUpdateFormDefinitionValidator : Validator<PartialUpdateFormDefinitionRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public PartialUpdateFormDefinitionValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.DefinitionId)
            .GreaterThan(0);
    }
}
