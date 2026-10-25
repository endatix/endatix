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
/// Confirms, resumes or cancels a Participant import.
/// </summary>
public sealed class Move(IMediator mediator, ITenantContext tenantContext)
    : Endpoint<MoveAudienceImportRequest, Results<Ok<AudienceImportResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Patch("forms/{formId}/audience/imports/{importId}");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Confirm, resume or cancel a Participant import";
            summary.Description = "status writing confirms a checked import, or resumes a stopped write from where " +
                "it stopped; status canceled stops an import before it writes anything.";
            summary.Responses[200] = "Import moved.";
            summary.Responses[400] = "Status is not writing or canceled.";
            summary.Responses[404] = "Import not found.";
            summary.Responses[409] = "The import cannot move to that status now.";
            summary.Responses[503] = "The form's audience is busy; try again shortly.";
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
        MoveAudienceImportRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new MoveImportCommand(new ImportRef(tenantContext.TenantId, request.FormId, request.ImportId), request.Status!),
            ct);

        return TypedResultsBuilder
            .MapResult(result, AudienceImportResponse.FromDto)
            .SetTypedResults<Ok<AudienceImportResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for moving a Participant import.
/// </summary>
public sealed class MoveAudienceImportValidator : Validator<MoveAudienceImportRequest>
{
    /// <summary>Builds the rules.</summary>
    public MoveAudienceImportValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.ImportId).GreaterThan(0);
        RuleFor(request => request.Status)
            .Must(status => status is AudienceImportStatusCodes.Writing or AudienceImportStatusCodes.Canceled)
            .WithMessage("Status must be writing or canceled.");
    }
}

/// <summary>
/// Request to move a Participant import.
/// </summary>
public sealed class MoveAudienceImportRequest
{
    /// <summary>The form.</summary>
    public long FormId { get; init; }

    /// <summary>The import.</summary>
    public long ImportId { get; init; }

    /// <summary>writing or canceled.</summary>
    public string? Status { get; init; }
}
