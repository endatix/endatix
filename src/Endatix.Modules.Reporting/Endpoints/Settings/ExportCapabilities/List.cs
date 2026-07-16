using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Reporting.Contracts.Export;
using FastEndpoints;

namespace Endatix.Modules.Reporting.Endpoints.Settings.ExportCapabilities;

public sealed class List(IExportCapabilityRegistry capabilityRegistry)
    : EndpointWithoutRequest<IReadOnlyList<ExportCapabilityDto>>
{
    public override void Configure()
    {
        Get("settings/export-capabilities");
        Permissions(Actions.Tenant.ManageSettings, Actions.Submissions.Export);
        Summary(summary =>
        {
            summary.Summary = "List supported export capabilities";
            summary.Responses[200] = "Supported export target, delivery format, and profile combinations.";
        });
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        IReadOnlyList<ExportCapabilityDto> capabilities = capabilityRegistry
            .GetAll()
            .Select(capability => new ExportCapabilityDto(
                capability.Target,
                capability.DeliveryFormat,
                capability.Profile,
                capability.WireKey,
                capability.Label,
                capability.ItemTypeName,
                capability.Description))
            .ToList();

        return Send.OkAsync(capabilities, ct);
    }
}
