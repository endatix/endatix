using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.FormDefinitions;

/// <summary>
/// Validation rules for the <c>UpdateActiveFormDefinitionRequest</c> class.
/// </summary>
public class UpdateActiveFormDefinitionValidator : Validator<UpdateActiveFormDefinitionRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public UpdateActiveFormDefinitionValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.IsDraft)
            .NotEmpty();

        RuleFor(x => x.JsonData)
            .NotEmpty();

        RuleFor(x => x.IsActive)
            .NotEmpty();
    }
}
