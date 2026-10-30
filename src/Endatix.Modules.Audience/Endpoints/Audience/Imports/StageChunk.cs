using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Imports;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.Imports;

/// <summary>
/// Stores one chunk of an uploading Participant import.
/// </summary>
public sealed class StageChunk(IMediator mediator, ITenantContext tenantContext)
    : Endpoint<StageAudienceImportChunkRequest, Results<Ok<AudienceImportResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Put("forms/{formId}/audience/imports/{importId}/chunks/{index}");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Send a chunk of a Participant import";
            summary.Description = "Stores one chunk of rows (chunkSize rows, fewer in the last chunk). Sending a chunk " +
                "again replaces it. The chunk that completes the file starts the Check.";
            summary.Responses[200] = "Chunk stored.";
            summary.Responses[400] = "The chunk's index, row count or cells do not fit the import.";
            summary.Responses[404] = "Import not found.";
            summary.Responses[409] = "The import is no longer uploading.";
            summary.Responses[503] = "The form's audience is busy; send the chunk again shortly.";
        });
        Description(builder => builder
            .Produces<AudienceImportResponse>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(503));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<AudienceImportResponse>, ProblemHttpResult>> ExecuteAsync(
        StageAudienceImportChunkRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new StageImportChunkCommand(
                new ImportRef(tenantContext.TenantId, request.FormId, request.ImportId),
                request.Index,
                request.FirstRowNumber,
                (request.Rows ?? []).Select(row => new StagedRow(row.Cells ?? [], row.Malformed)).ToList()),
            ct);

        return TypedResultsBuilder
            .MapResult(result, AudienceImportResponse.FromDto)
            .SetTypedResults<Ok<AudienceImportResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for one chunk of a Participant import.
/// </summary>
public sealed class StageAudienceImportChunkValidator : Validator<StageAudienceImportChunkRequest>
{
    /// <summary>Longest cell a chunk accepts.</summary>
    public const int MaxCellLength = 4_000;

    /// <summary>Builds the rules.</summary>
    public StageAudienceImportChunkValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.ImportId).GreaterThan(0);
        RuleFor(request => request.Index).GreaterThanOrEqualTo(0);
        RuleFor(request => request.FirstRowNumber).GreaterThanOrEqualTo(2);
        RuleFor(request => request.Rows).NotEmpty();
        RuleForEach(request => request.Rows).ChildRules(row =>
        {
            row.RuleFor(item => item.Cells).NotNull();
            row.RuleForEach(item => item.Cells).MaximumLength(MaxCellLength);
        });
    }
}

/// <summary>
/// One chunk of a Participant import: consecutive data rows, the first at <see cref="FirstRowNumber"/>.
/// </summary>
public sealed class StageAudienceImportChunkRequest
{
    /// <summary>The form.</summary>
    public long FormId { get; init; }

    /// <summary>The import.</summary>
    public long ImportId { get; init; }

    /// <summary>The chunk's place in the file, from 0.</summary>
    public int Index { get; init; }

    /// <summary>The spreadsheet row of the first row; row 1 is the header row.</summary>
    public int FirstRowNumber { get; init; }

    /// <summary>The chunk's rows, in order.</summary>
    public List<StagedRowRequest>? Rows { get; init; }
}

/// <summary>
/// One data row: its cells in header order, and whether the browser's parser found a stray quote in it.
/// </summary>
public sealed class StagedRowRequest
{
    /// <summary>The row's cells, in header order.</summary>
    public List<string>? Cells { get; init; }

    /// <summary>Whether the browser's parser found a stray quote in the row.</summary>
    public bool Malformed { get; init; }
}
