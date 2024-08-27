using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Validation rules for the <c>UpdateFormDefinitionRequest</c> class.
/// </summary>
public class UpdateFormDefinitionValidator : Validator<UpdateFormDefinitionRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public UpdateFormDefinitionValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.DefinitionId)
            .GreaterThan(0);

        RuleFor(x => x.IsDraft)
            .NotEmpty();

        RuleFor(x => x.JsonData)
            .NotEmpty();

        RuleFor(x => x.IsActive)
            .NotEmpty();
    }
}
