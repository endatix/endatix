using System.Text.Json;
using Endatix.Api.Common;
using Endatix.Infrastructure.Data.Config;
using FastEndpoints;
using FluentValidation;

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

        // Accept either FormDefinitionSchema or the obsolete FormDefinitionJsonData HTTP field.
#pragma warning disable CS0618
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.FormDefinitionJsonData) || x.FormDefinitionSchema.HasValue)
            .WithMessage("Either FormDefinitionJsonData or FormDefinitionSchema must be provided.")
            .WithName("FormDefinition");

        RuleFor(x => x.FormDefinitionJsonData)
            .MinimumLength(DataSchemaConstants.MIN_JSON_LENGTH)
            .When(x => x.FormDefinitionJsonData != null);
#pragma warning restore CS0618

        RuleFor(x => x.FormDefinitionSchema)
            .Must(schema => schema!.Value.ValueKind == JsonValueKind.Object)
            .WithMessage("FormDefinitionSchema must be a JSON object.")
            .When(x => x.FormDefinitionSchema.HasValue);

        RuleFor(x => x.Metadata)
            .ValidJsonString()
            .When(x => x.Metadata != null);
    }
}
