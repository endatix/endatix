using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.ExportFormats;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Endpoints.Settings.ExportFormats;

public sealed class List(
    IMediator mediator,
    ITenantContext tenantContext)
    : EndpointWithoutRequest<Results<Ok<List<ExportFormatDto>>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("settings/export-formats");
        Permissions(Actions.Tenant.ManageSettings, Actions.Submissions.Export);
        Summary(summary =>
        {
            summary.Summary = "List tenant export formats";
            summary.Description = "Returns export format definitions configured for the current tenant.";
            summary.ResponseExamples[200] = new List<ExportFormatDto>
            {
                new(
                    Id: 1,
                    Name: "CSV Export",
                    ExportTarget: Contracts.Export.ExportTarget.Submissions,
                    DeliveryFormat: Contracts.Export.ExportDeliveryFormat.Csv,
                    Profile: Contracts.Export.ExportProfile.Native,
                    WireKey: "csv",
                    Label: "CSV",
                    Description: "Standard CSV export format",
                    Settings: new ExportFormatSettings(),
                    CreatedAt: DateTime.UtcNow,
                    ModifiedAt: null)
            };
            summary.Responses[200] = "Export formats retrieved.";
        });
        Description(builder => builder
            .Produces<List<ExportFormatDto>>(200, "application/json"));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<List<ExportFormatDto>>, ProblemHttpResult>> ExecuteAsync(
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new ListExportFormatsQuery(tenantContext.TenantId),
            ct);

        return TypedResultsBuilder
            .MapResult(result, formats => formats)
            .SetTypedResults<Ok<List<ExportFormatDto>>, ProblemHttpResult>();
    }
}
