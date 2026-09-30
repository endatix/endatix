using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Common;
using Endatix.Modules.Audience.Features.Import;
using FastEndpoints;
using FluentValidation;
using MediatR;
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

    public override void Configure()
    {
        Post("forms/{formId}/audience/import");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Import audience CSV";
            summary.Description =
                "Imports up to 5,000 people synchronously. Supports column mapping and " +
                "variable__choice multi-choice columns. Partial success is returned with counts.";
            summary.Responses[200] = "Import finished (may include rejections).";
            summary.Responses[400] = "Invalid CSV or mapping.";
            summary.Responses[404] = "Form not found.";
        });
        Description(builder => builder
            .Produces<AudienceImportResponse>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    public override async Task<Results<Ok<AudienceImportResponse>, ProblemHttpResult>> ExecuteAsync(
        ImportAudienceCsvRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new ImportCsvCommand(
                tenantContext.TenantId,
                request.FormId,
                request.CsvText!,
                request.IdentifierColumn!,
                string.IsNullOrWhiteSpace(request.FileName) ? DefaultFileName : request.FileName,
                request.PropertyColumns,
                long.TryParse(userContext.GetCurrentUserId(), out long userId) ? userId : null),
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
        Rejections = dto.Rejections
            .Select(rejection => new AudienceImportRejectionResponse
            {
                RowNumber = rejection.RowNumber,
                Reason = rejection.Reason,
            })
            .ToList(),
    };
}

/// <summary>
/// One rejected row in an import response.
/// </summary>
public sealed class AudienceImportRejectionResponse
{
    public int RowNumber { get; init; }

    public string Reason { get; init; } = string.Empty;
}
