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
/// Reads the form's unfinished Participant import, so a scripter who comes back sees where it stands.
/// </summary>
public sealed class GetCurrent(IMediator mediator, ITenantContext tenantContext)
    : Endpoint<GetCurrentAudienceImportRequest, Results<Ok<AudienceImportResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("forms/{formId}/audience/imports/current");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Get the form's unfinished Participant import";
            summary.Description = "Returns the import that is uploading, checking, checked, writing, or a stopped write that can resume.";
            summary.Responses[200] = "Import retrieved.";
            summary.Responses[404] = "The form has no unfinished import.";
        });
        Description(builder => builder
            .Produces<AudienceImportResponse>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<AudienceImportResponse>, ProblemHttpResult>> ExecuteAsync(
        GetCurrentAudienceImportRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetImportQuery(tenantContext.TenantId, request.FormId, null), ct);

        return TypedResultsBuilder
            .MapResult(result, AudienceImportResponse.FromDto)
            .SetTypedResults<Ok<AudienceImportResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for reading the form's unfinished Participant import.
/// </summary>
public sealed class GetCurrentAudienceImportValidator : Validator<GetCurrentAudienceImportRequest>
{
    /// <summary>Builds the rules.</summary>
    public GetCurrentAudienceImportValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
    }
}

/// <summary>
/// Request to read the form's unfinished Participant import.
/// </summary>
public sealed class GetCurrentAudienceImportRequest
{
    /// <summary>The form.</summary>
    public long FormId { get; init; }
}
