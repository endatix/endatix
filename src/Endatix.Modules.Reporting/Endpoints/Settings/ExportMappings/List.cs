using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.ExportMappings;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Endpoints.Settings.ExportMappings;

/// <summary>
/// Lists the export mappings for the current tenant.
/// </summary>
public sealed class List(
    IMediator mediator,
    ITenantContext tenantContext)
    : EndpointWithoutRequest<Results<Ok<List<ExportMappingDto>>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("settings/export-mappings");
        Permissions(Actions.Tenant.ManageSettings);
        Summary(summary =>
        {
            summary.Summary = "List tenant export mappings";
            summary.Description = "Returns export mappings configured for the current tenant.";
            summary.ResponseExamples[200] = new List<ExportMappingDto>
            {
                new(
                    Id: 1,
                    ExportFormatId: 1,
                    SurveyTypeId: null,
                    IsDefault: true,
                    ExportFormat: null)
            };
            summary.Responses[200] = "Export mappings retrieved.";
        });
        Description(builder => builder
            .Produces<List<ExportMappingDto>>(200, "application/json"));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<List<ExportMappingDto>>, ProblemHttpResult>> ExecuteAsync(
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new ListExportMappingsQuery(tenantContext.TenantId),
            ct);

        return TypedResultsBuilder
            .MapResult(result, mappings => mappings)
            .SetTypedResults<Ok<List<ExportMappingDto>>, ProblemHttpResult>();
    }
}
