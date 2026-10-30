using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Features.Imports;
using Endatix.Modules.Audience.Shared;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.Imports;

/// <summary>
/// Lists every row a Participant import did not write, for the Hub's rejected-rows download.
/// </summary>
public sealed class ListRejections(IMediator mediator, ITenantContext tenantContext)
    : Endpoint<ListAudienceImportRejectionsRequest, Results<Ok<Paged<AudienceImportRejectedRowResponse>>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("forms/{formId}/audience/imports/{importId}/rejections");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "List an import's rejected rows";
            summary.Description = "Lists every row the import did not write, by spreadsheet row, with the reason and " +
                "its cells as uploaded in header order, at most 5,000 a page. Rows are kept until 7 days after the " +
                "import finished.";
            summary.Responses[200] = "Rejected rows retrieved.";
            summary.Responses[400] = "Invalid paging.";
            summary.Responses[404] = "Import not found.";
        });
        Description(builder => builder
            .Produces<Paged<AudienceImportRejectedRowResponse>>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<Paged<AudienceImportRejectedRowResponse>>, ProblemHttpResult>> ExecuteAsync(
        ListAudienceImportRejectionsRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new ListImportRejectionsQuery(
                tenantContext.TenantId, request.FormId, request.ImportId, request.Page, request.PageSize),
            ct);

        return TypedResultsBuilder
            .MapResult(result, Map)
            .SetTypedResults<Ok<Paged<AudienceImportRejectedRowResponse>>, ProblemHttpResult>();
    }

    private static Paged<AudienceImportRejectedRowResponse> Map(Paged<ImportRejectedRowDto> paged) =>
        new(
            paged.Page,
            paged.PageSize,
            paged.TotalRecords,
            paged.TotalPages,
            paged.Items.Select(AudienceImportRejectedRowResponse.FromDto).ToList());
}

/// <summary>
/// Validator for listing an import's rejected rows.
/// </summary>
public sealed class ListAudienceImportRejectionsValidator : Validator<ListAudienceImportRejectionsRequest>
{
    /// <summary>Builds the rules.</summary>
    public ListAudienceImportRejectionsValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.ImportId).GreaterThan(0);
        RuleFor(request => request.Page)
            .GreaterThan(0)
            .When(request => request.Page.HasValue);
        RuleFor(request => request.PageSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(AudiencePaging.MaxPageSize)
            .When(request => request.PageSize.HasValue);
    }
}

/// <summary>
/// Request to list an import's rejected rows.
/// </summary>
public sealed class ListAudienceImportRejectionsRequest
{
    /// <summary>The form.</summary>
    public long FormId { get; init; }

    /// <summary>The import.</summary>
    public long ImportId { get; init; }

    /// <summary>The page, from 1.</summary>
    public int? Page { get; init; }

    /// <summary>Rows per page: 50 by default, at most 5,000.</summary>
    public int? PageSize { get; init; }
}

/// <summary>
/// One rejected row of an import.
/// </summary>
public sealed class AudienceImportRejectedRowResponse
{
    /// <summary>The spreadsheet row; row 1 is the header row.</summary>
    public int RowNumber { get; init; }

    /// <summary>Why the row was not written.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>The row's cells as uploaded, in the order of the import's headers.</summary>
    public IReadOnlyList<string> Cells { get; init; } = [];

    internal static AudienceImportRejectedRowResponse FromDto(ImportRejectedRowDto dto) => new()
    {
        RowNumber = dto.RowNumber,
        Reason = dto.Reason,
        Cells = dto.Cells,
    };
}
