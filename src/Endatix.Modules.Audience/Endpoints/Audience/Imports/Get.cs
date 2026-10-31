using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Audience.Features.Imports;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.Imports;

/// <summary>
/// Reads one Participant import, which the Hub polls while it runs.
/// </summary>
public sealed class Get(IMediator mediator, ITenantContext tenantContext)
    : Endpoint<GetAudienceImportRequest, Results<Ok<AudienceImportResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("forms/{formId}/audience/imports/{importId}");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Get a Participant import";
            summary.Description = "Returns the import's status, the rows checked and written so far, the Check's " +
                "counts once it has read every row, and the first 50 rejected rows.";
            summary.Responses[200] = "Import retrieved.";
            summary.Responses[404] = "Import not found.";
        });
        Description(builder => builder
            .Produces<AudienceImportResponse>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<AudienceImportResponse>, ProblemHttpResult>> ExecuteAsync(
        GetAudienceImportRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetImportQuery(tenantContext.TenantId, request.FormId, request.ImportId), ct);

        return TypedResultsBuilder
            .MapResult(result, AudienceImportResponse.FromDto)
            .SetTypedResults<Ok<AudienceImportResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for reading a Participant import.
/// </summary>
public sealed class GetAudienceImportValidator : Validator<GetAudienceImportRequest>
{
    /// <summary>Builds the rules.</summary>
    public GetAudienceImportValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.ImportId).GreaterThan(0);
    }
}

/// <summary>
/// Request to read a Participant import.
/// </summary>
public sealed class GetAudienceImportRequest
{
    /// <summary>The form.</summary>
    public long FormId { get; init; }

    /// <summary>The import.</summary>
    public long ImportId { get; init; }
}
