using System.Text.Json;
using Endatix.Api.Common;
using Endatix.Infrastructure.Data.Config;
using FastEndpoints;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

// An alias, not the namespace: Endatix.Core.Common also has a DataSchemaConstants, which would clash with the one used here.
using OutboundAddressPolicy = Endatix.Core.Common.OutboundAddressPolicy;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Validation rules for the <c>CreateFormRequest</c> class.
/// </summary>
public class CreateFormValidator : Validator<CreateFormRequest>
{
    /// <summary>
    /// Validator without the server's settings: endpoint URLs on private and local networks are refused.
    /// </summary>
    public CreateFormValidator()
        : this(OutboundAddressPolicy.PublicOnly)
    {
    }

    /// <summary>
    /// Validator the endpoint uses, built with the server's <see cref="OutboundAddressPolicy"/>.
    /// </summary>
    /// <param name="outboundAddressPolicy">Whether webhook endpoints may be on private and local networks.</param>
    [ActivatorUtilitiesConstructor]
    public CreateFormValidator(OutboundAddressPolicy outboundAddressPolicy)
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MinimumLength(DataSchemaConstants.MIN_NAME_LENGTH)
            .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH);

        RuleFor(x => x.IsEnabled)
            .NotEmpty();

        RuleForFormDefinition();

        RuleFor(x => x.Metadata)
            .ValidJsonString()
            .When(x => x.Metadata != null);

        RuleForWebHookSettings(outboundAddressPolicy);
    }

    // Accept either FormDefinitionSchema or the obsolete FormDefinitionJsonData HTTP field.
    private void RuleForFormDefinition()
    {
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
    }

    private void RuleForWebHookSettings(OutboundAddressPolicy outboundAddressPolicy)
    {
        RuleFor(x => x.WebHookSettings)
            .PublicWebHookEndpoints(outboundAddressPolicy);
#pragma warning disable CS0618 // The obsolete string field is still accepted, so it is checked the same way.
        RuleFor(x => x.WebHookSettingsJson)
            .PublicWebHookEndpoints(outboundAddressPolicy);
#pragma warning restore CS0618
    }
}
