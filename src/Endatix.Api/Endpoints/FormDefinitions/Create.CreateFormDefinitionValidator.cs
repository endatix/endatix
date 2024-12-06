using FastEndpoints;
using FluentValidation;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Validation rules for the <c>CreateFormDefinitionRequest</c> class.
/// </summary>
public class CreateFormDefinitionValidator : Validator<CreateFormDefinitionRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public CreateFormDefinitionValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.IsDraft)
            .NotEmpty();

        RuleFor(x => x.JsonData)
            .NotEmpty()
            .MinimumLength(DataSchemaConstants.MIN_JSON_LENGTH);
    }
}
