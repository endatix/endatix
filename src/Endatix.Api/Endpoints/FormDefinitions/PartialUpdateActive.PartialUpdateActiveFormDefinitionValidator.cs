using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.FormDefinitions;

/// <summary>
/// Validation rules for the <c>PartialUpdateActiveFormDefinitionRequest</c> class.
/// </summary>
public class PartialUpdateActiveFormDefinitionValidator : Validator<PartialUpdateActiveFormDefinitionRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public PartialUpdateActiveFormDefinitionValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);
    }
}