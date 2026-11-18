using Endatix.Api.Common;
using Endatix.Core.Common;
using FastEndpoints;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Validation rules for the <c>PartialUpdateFormRequest</c> class.
/// </summary>
public class PartialUpdateFormValidator : Validator<PartialUpdateFormRequest>
{
    /// <summary>
    /// Validator without the server's settings: endpoint URLs on private and local networks are refused.
    /// </summary>
    public PartialUpdateFormValidator()
        : this(OutboundAddressPolicy.PublicOnly)
    {
    }

    /// <summary>
    /// Validator the endpoint uses, built with the server's <see cref="OutboundAddressPolicy"/>.
    /// </summary>
    /// <param name="outboundAddressPolicy">Whether webhook endpoints may be on private and local networks.</param>
    [ActivatorUtilitiesConstructor]
    public PartialUpdateFormValidator(OutboundAddressPolicy outboundAddressPolicy)
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.Metadata)
            .ValidJsonString()
            .When(x => x.Metadata != null);

        RuleFor(x => x.WebHookSettingsJson)
            .NoRetiredWebHookKeys()
            .PublicWebHookEndpoints(outboundAddressPolicy);
    }
}
