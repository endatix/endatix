using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.ExportFormats;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Endpoints.Settings.ExportFormats;

public sealed class PartialUpdate(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<PartialUpdateExportFormatRequest, Results<Ok<ExportFormatDto>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Patch("settings/export-formats/{exportFormatId}");
        Permissions(Actions.Tenant.ManageSettings);
        Summary(summary =>
        {
            summary.Summary = "Partial update tenant export format";
            summary.Description = "Partially updates an existing export format configuration.";
            summary.ExampleRequest = new PartialUpdateExportFormatRequest
            {
                ExportFormatId = 1,
                Name = "CSV Export",
                Description = "Updated description"
            };
            summary.ResponseExamples[200] = new ExportFormatDto(
                Id: 1,
                Name: "CSV Export",
                ExportTarget: Contracts.Export.ExportTarget.Submissions,
                DeliveryFormat: Contracts.Export.ExportDeliveryFormat.Csv,
                Profile: Contracts.Export.ExportProfile.Native,
                WireKey: "csv",
                Label: "CSV",
                Description: "Updated description",
                Settings: new ExportFormatSettings(),
                CreatedAt: DateTime.UtcNow,
                ModifiedAt: DateTime.UtcNow);
            summary.Responses[200] = "Export format updated.";
            summary.Responses[404] = "Export format not found.";
        });
        Description(builder => builder
            .Produces<ExportFormatDto>(200, "application/json")
            .ProducesProblem(404));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<ExportFormatDto>, ProblemHttpResult>> ExecuteAsync(
        PartialUpdateExportFormatRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new UpdateExportFormatCommand(
                tenantContext.TenantId,
                request.ExportFormatId,
                request.Name,
                request.Description,
                request.Settings),
            ct);

        return TypedResultsBuilder
            .MapResult(result, format => format)
            .SetTypedResults<Ok<ExportFormatDto>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validates the request for the partial update export format endpoint.
/// </summary>
public sealed class PartialUpdateExportFormatValidator : Validator<PartialUpdateExportFormatRequest>
{
    public PartialUpdateExportFormatValidator()
    {
        RuleFor(request => request.ExportFormatId).GreaterThan(0);

        RuleFor(request => request.Name)
            .MaximumLength(ExportFormat.NAME_MAX_LENGTH)
            .When(request => request.Name is not null);

        RuleFor(request => request.Description)
            .MaximumLength(ExportFormat.DESCRIPTION_MAX_LENGTH)
            .When(request => request.Description is not null);
    }
}

/// <summary>
/// The request for the partial update export format endpoint.
/// </summary>
public sealed class PartialUpdateExportFormatRequest
{
    public long ExportFormatId { get; init; }

    public string? Name { get; init; }

    public string? Description { get; init; }

    public ExportFormatSettingsInput? Settings { get; init; }
}
