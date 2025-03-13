using Endatix.Infrastructure.Data.Config;
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Validation rules for the <c>CreateFormTemplateRequest</c> class.
/// </summary>
public class CreateFormTemplateValidator : Validator<CreateFormTemplateRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public CreateFormTemplateValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MinimumLength(DataSchemaConstants.MIN_NAME_LENGTH)
            .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH);

        RuleFor(x => x.IsEnabled)
            .NotEmpty();

        RuleFor(x => x.JsonData)
            .NotEmpty()
            .MinimumLength(DataSchemaConstants.MIN_JSON_LENGTH);
    }
}
