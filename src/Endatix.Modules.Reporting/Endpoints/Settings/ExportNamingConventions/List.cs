using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Reporting.Contracts.Export;
using FastEndpoints;

namespace Endatix.Modules.Reporting.Endpoints.Settings.ExportNamingConventions;

public sealed class List(IColumnAliasTransformerRegistry aliasTransformerRegistry)
    : EndpointWithoutRequest<IReadOnlyList<ColumnAliasNamingConventionDto>>
{
    public override void Configure()
    {
        Get("settings/export-naming-conventions");
        Permissions(Actions.Tenant.ManageSettings, Actions.Submissions.Export);
        Summary(summary =>
        {
            summary.Summary = "List column naming conventions";
            summary.Responses[200] =
                "Registered column naming conventions with labels, descriptions, and examples.";
        });
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        return Send.OkAsync(aliasTransformerRegistry.GetCatalog(), ct);
    }
}
