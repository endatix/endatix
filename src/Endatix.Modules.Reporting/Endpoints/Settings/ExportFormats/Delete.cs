using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Features.ExportFormats;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Endpoints.Settings.ExportFormats;

public sealed class Delete(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<DeleteExportFormatRequest, Results<Ok<string>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Delete("settings/export-formats/{exportFormatId}");
        Permissions(Actions.Tenant.ManageSettings);
        Summary(summary =>
        {
            summary.Summary = "Delete tenant export format";
            summary.Description = "Deletes an export format configuration by its ID.";
            summary.ExampleRequest = new DeleteExportFormatRequest { ExportFormatId = 1 };
            summary.ResponseExamples[200] = new { Message = "Export format deleted successfully." };
            summary.Responses[200] = "Export format deleted.";
            summary.Responses[404] = "Export format not found.";
        });
        Description(builder => builder
            .Produces<string>(200, "application/json")
            .ProducesProblem(404));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<string>, ProblemHttpResult>> ExecuteAsync(
        DeleteExportFormatRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new DeleteExportFormatCommand(tenantContext.TenantId, request.ExportFormatId),
            ct);

        return TypedResultsBuilder
            .MapResult(result, id => id)
            .SetTypedResults<Ok<string>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validates the request for the delete export format endpoint.
/// </summary>
public sealed class DeleteExportFormatValidator : Validator<DeleteExportFormatRequest>
{
    public DeleteExportFormatValidator()
    {
        RuleFor(request => request.ExportFormatId).GreaterThan(0);
    }
}

/// <summary>
/// The request for the delete export format endpoint.
/// </summary>
public sealed class DeleteExportFormatRequest
{
    public long ExportFormatId { get; init; }
}
