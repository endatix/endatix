using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.FormDefinitions;

/// <summary>
/// Validation rules for the <c>GetActiveFormDefinitionRequest</c> class.
/// </summary>
public class GetActiveFormDefinitionValidator : Validator<GetActiveFormDefinitionRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public GetActiveFormDefinitionValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);
    }
}
