using FastEndpoints;
using FluentValidation;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Validation rules for the <c>CreateFormRequest</c> class.
/// </summary>
public class CreateFormValidator : Validator<CreateFormRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public CreateFormValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MinimumLength(DataSchemaConstants.MIN_NAME_LENGTH)
            .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH);

        RuleFor(x => x.IsEnabled)
            .NotEmpty();

        RuleFor(x => x.FormDefinitionJsonData)
            .NotEmpty()
            .MinimumLength(DataSchemaConstants.MIN_JSON_LENGTH);
    }
}
