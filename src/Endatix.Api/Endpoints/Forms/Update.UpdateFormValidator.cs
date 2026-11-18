using FastEndpoints;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Endatix.Infrastructure.Data.Config;
using Endatix.Api.Common;

// An alias, not the namespace: Endatix.Core.Common also has a DataSchemaConstants, which would clash with the one used here.
using OutboundAddressPolicy = Endatix.Core.Common.OutboundAddressPolicy;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Validation rules for the <c>UpdateFormRequest</c> class.
/// </summary>
public class UpdateFormValidator : Validator<UpdateFormRequest>
{
    /// <summary>
    /// Validator without the server's settings: endpoint URLs on private and local networks are refused.
    /// </summary>
    public UpdateFormValidator()
        : this(OutboundAddressPolicy.PublicOnly)
    {
    }

    /// <summary>
    /// Validator the endpoint uses, built with the server's <see cref="OutboundAddressPolicy"/>.
    /// </summary>
    /// <param name="outboundAddressPolicy">Whether webhook endpoints may be on private and local networks.</param>
    [ActivatorUtilitiesConstructor]
    public UpdateFormValidator(OutboundAddressPolicy outboundAddressPolicy)
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MinimumLength(DataSchemaConstants.MIN_NAME_LENGTH)
            .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH);

        RuleFor(x => x.IsEnabled)
            .NotEmpty();

        RuleFor(x => x.Metadata)
            .ValidJsonString()
            .When(x => x.Metadata != null);

        RuleFor(x => x.WebHookSettingsJson)
            .NoRetiredWebHookKeys()
            .PublicWebHookEndpoints(outboundAddressPolicy);
    }
}
