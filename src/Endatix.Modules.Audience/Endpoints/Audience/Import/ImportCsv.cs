using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Common;
using Endatix.Modules.Audience.Features.Import;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.Import;

/// <summary>
/// Sync-imports people into a form audience from CSV (max 5,000 rows).
/// </summary>
public sealed class ImportCsv(
    IMediator mediator,
    ITenantContext tenantContext,
    IUserContext userContext)
    : Endpoint<ImportAudienceCsvRequest, Results<Ok<AudienceImportResponse>, ProblemHttpResult>>
{
    private const string DefaultFileName = "audience.csv";

    private const string ImportDescription = "Imports up to 5,000 people synchronously, with column mapping and " +
        "variable__choice multi-choice columns; partial success returns counts. With isTest, people new to " +
        "this form are added as test persons, and people already on it get no writes and count as skipped.";

    public override void Configure()
    {
        Post("forms/{formId}/audience/import");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Import audience CSV";
            summary.Description = ImportDescription;
            summary.Responses[200] = "Import finished (may include rejections).";
            summary.Responses[400] = "Invalid CSV or mapping.";
            summary.Responses[404] = "Form not found.";
            summary.Responses[409] =
                "The form has an import in progress (importId), or the audience changed while the import ran.";
            summary.Responses[503] = "The form's people are being imported or changed; try again shortly.";
        });
        Description(Produces);
    }

    private static void Produces(RouteHandlerBuilder builder) =>
        builder
            .Produces<AudienceImportResponse>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(503);

    public override async Task<Results<Ok<AudienceImportResponse>, ProblemHttpResult>> ExecuteAsync(
        ImportAudienceCsvRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new ImportCsvCommand(
                tenantContext.TenantId,
                request.FormId,
                request.CsvText ?? string.Empty,
                request.IdentifierColumn ?? string.Empty,
                string.IsNullOrWhiteSpace(request.FileName) ? DefaultFileName : request.FileName,
                request.PropertyColumns,
                long.TryParse(userContext.GetCurrentUserId(), out long userId) ? userId : null,
                IsTest: request.IsTest),
            ct);

        return TypedResultsBuilder
            .MapResult(result, AudienceImportResponse.FromDto)
            .SetTypedResults<Ok<AudienceImportResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for audience CSV import.
/// </summary>
public sealed class ImportAudienceCsvValidator : Validator<ImportAudienceCsvRequest>
{
    public ImportAudienceCsvValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.CsvText).NotEmpty();
        RuleFor(request => request.IdentifierColumn).NotEmpty();
        RuleFor(request => request.FileName).MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH);
    }
}

/// <summary>
/// Request body for audience CSV import.
/// </summary>
public sealed class ImportAudienceCsvRequest
{
    public long FormId { get; init; }

    public string? CsvText { get; init; }

    public string? IdentifierColumn { get; init; }

    public string? FileName { get; init; }

    /// <summary>Maps property <c>variableName</c> → CSV header for simple columns.</summary>
    public Dictionary<string, string>? PropertyColumns { get; init; }

    /// <summary>
    /// Adds the people new to this form as test persons. People already on it get no writes (flag and values) and
    /// are counted as skipped.
    /// </summary>
    public bool IsTest { get; init; }
}

/// <summary>
/// Wire model for an audience CSV import result.
/// </summary>
public sealed class AudienceImportResponse
{
    public long ImportId { get; init; }

    public int CreatedCount { get; init; }

    public int UpdatedCount { get; init; }

    public int SkippedCount { get; init; }

    public int RejectedCount { get; init; }

    public IReadOnlyList<AudienceImportRejectionResponse> Rejections { get; init; } = [];

    internal static AudienceImportResponse FromDto(ImportResultDto dto) => new()
    {
        ImportId = dto.ImportId,
        CreatedCount = dto.CreatedCount,
        UpdatedCount = dto.UpdatedCount,
        SkippedCount = dto.SkippedCount,
        RejectedCount = dto.RejectedCount,
        Rejections = [.. dto.Rejections.Select(AudienceImportRejectionResponse.FromDto)],
    };
}

/// <summary>
/// One rejected row in an import response.
/// </summary>
public sealed class AudienceImportRejectionResponse
{
    public int RowNumber { get; init; }

    public string Reason { get; init; } = string.Empty;

    internal static AudienceImportRejectionResponse FromDto(ImportRejectionDto dto) => new()
    {
        RowNumber = dto.RowNumber,
        Reason = dto.Reason,
    };
}
