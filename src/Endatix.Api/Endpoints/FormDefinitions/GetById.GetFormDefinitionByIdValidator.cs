using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Validation rules for the <c>GetFormDefinitionByIdRequest</c> class.
/// </summary>
public class GetFormDefinitionByIdValidator : Validator<GetFormDefinitionByIdRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public GetFormDefinitionByIdValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.DefinitionId)
            .GreaterThan(0);
    }
}
