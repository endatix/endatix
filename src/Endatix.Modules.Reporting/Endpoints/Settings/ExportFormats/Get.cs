using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.ExportFormats;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Endpoints.Settings.ExportFormats;

public sealed class Get(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<GetExportFormatRequest, Results<Ok<ExportFormatDto>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("settings/export-formats/{exportFormatId}");
        Permissions(Actions.Tenant.ManageSettings);
        Summary(summary =>
        {
            summary.Summary = "Get tenant export format";
            summary.Description = "Retrieves an export format configuration by its ID.";
            summary.ExampleRequest = new GetExportFormatRequest { ExportFormatId = 1 };
            summary.ResponseExamples[200] = new ExportFormatDto(
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
                ModifiedAt: null,
                AllowedFilters: AllowedExportFilters.ToAllowedFilterNames(ExportRequestFilterSets.Submissions));
            summary.Responses[200] = "Export format retrieved.";
            summary.Responses[404] = "Export format not found.";
        });
        Description(builder => builder
            .Produces<ExportFormatDto>(200, "application/json")
            .ProducesProblem(404));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<ExportFormatDto>, ProblemHttpResult>> ExecuteAsync(
        GetExportFormatRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new GetExportFormatQuery(tenantContext.TenantId, request.ExportFormatId),
            ct);

        return TypedResultsBuilder
            .MapResult(result, format => format)
            .SetTypedResults<Ok<ExportFormatDto>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validates the request for the get export format endpoint.
/// </summary>
public sealed class GetExportFormatValidator : Validator<GetExportFormatRequest>
{
    public GetExportFormatValidator()
    {
        RuleFor(request => request.ExportFormatId).GreaterThan(0);
    }
}

/// <summary>
/// The request for the get export format endpoint.
/// </summary>
public sealed class GetExportFormatRequest
{
    public long ExportFormatId { get; init; }
}
