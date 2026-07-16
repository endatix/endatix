using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.ExportMappings;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Endpoints.Settings.ExportMappings;

public sealed class Upsert(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<UpsertExportMappingEndpointRequest, Results<Ok<ExportMappingDto>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Put("settings/export-mappings");
        Permissions(Actions.Tenant.ManageSettings);
        Summary(summary =>
        {
            summary.Summary = "Upsert tenant export mapping";
            summary.Description = "Creates or updates an export mapping for the current tenant.";
            summary.ExampleRequest = new UpsertExportMappingEndpointRequest
            {
                ExportFormatId = 1,
                SurveyTypeId = null,
                IsDefault = true
            };
            summary.ResponseExamples[200] = new ExportMappingDto(
                Id: 1,
                ExportFormatId: 1,
                SurveyTypeId: null,
                IsDefault: true,
                ExportFormat: null);
            summary.Responses[200] = "Export mapping saved.";
            summary.Responses[404] = "Export format not found.";
        });
        Description(builder => builder
            .Produces<ExportMappingDto>(200, "application/json")
            .ProducesProblem(404));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<ExportMappingDto>, ProblemHttpResult>> ExecuteAsync(
        UpsertExportMappingEndpointRequest request,
        CancellationToken cancellationToken)
    {
        UpsertExportMappingRequest mappingRequest = new(
            request.ExportFormatId,
            request.SurveyTypeId,
            request.IsDefault);

        var result = await mediator.Send(
            new UpsertExportMappingCommand(tenantContext.TenantId, mappingRequest),
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, mapping => mapping)
            .SetTypedResults<Ok<ExportMappingDto>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validates the request for the upsert export mapping endpoint.
/// </summary>
public sealed class UpsertExportMappingValidator : Validator<UpsertExportMappingEndpointRequest>
{
    public UpsertExportMappingValidator()
    {
        RuleFor(request => request.ExportFormatId).GreaterThan(0);
    }
}

/// <summary>
/// The request for the upsert export mapping endpoint.
/// </summary>
public sealed class UpsertExportMappingEndpointRequest
{
    public long ExportFormatId { get; init; }

    public long? SurveyTypeId { get; init; }

    public bool IsDefault { get; init; }
}
