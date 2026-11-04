using Endatix.Core.Entities;
using FastEndpoints;
using FluentValidation;
using Endatix.Infrastructure.Data.Config;
using Endatix.Api.Common;

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

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.FormDefinitionJsonData) || x.FormDefinitionSchema.HasValue)
            .WithMessage("Either FormDefinitionJsonData or FormDefinitionSchema must be provided.")
            .WithName("FormDefinition");

        RuleFor(x => x.FormDefinitionJsonData)
            .MinimumLength(DataSchemaConstants.MIN_JSON_LENGTH)
            .When(x => x.FormDefinitionJsonData != null);

        RuleFor(x => x.Metadata)
            .ValidJsonString()
            .When(x => x.Metadata != null);

#pragma warning disable CS0618 // WebHookSettingsJson is obsolete but still accepted.
        RuleFor(x => x.WebHookSettingsJson)
            .Must(json => WebHookConfiguration.RetiredKeyError(json) is null)
            .WithMessage(x => WebHookConfiguration.RetiredKeyError(x.WebHookSettingsJson));
#pragma warning restore CS0618

        RuleFor(x => x.WebHookSettings)
            .Must(settings => settings is null || WebHookConfiguration.RetiredKeyError(settings.Value) is null)
            .WithMessage(x => WebHookConfiguration.RetiredKeyError(x.WebHookSettings!.Value));
    }
}
