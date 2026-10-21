using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Audience.Features.People;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.People;

/// <summary>
/// Reads one participant on this form, by member id, with their submissions on it.
/// </summary>
public sealed class Get(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<GetAudienceParticipantRequest, Results<Ok<AudienceParticipantResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("forms/{formId}/audience/people/{memberId}");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Get audience participant";
            summary.Description =
                "Reads one participant on this form by member id: values, Link status, and the newest 50 of their " +
                "submissions on the form with the total.";
            summary.Responses[200] = "The participant.";
            summary.Responses[404] = "Form not found, or the participant is not on this form.";
        });
        Description(builder => builder
            .Produces<AudienceParticipantResponse>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    public override async Task<Results<Ok<AudienceParticipantResponse>, ProblemHttpResult>> ExecuteAsync(
        GetAudienceParticipantRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new GetParticipantQuery(tenantContext.TenantId, request.FormId, request.MemberId),
            ct);

        return TypedResultsBuilder
            .MapResult(result, AudienceParticipantResponse.FromDto)
            .SetTypedResults<Ok<AudienceParticipantResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for get audience participant.
/// </summary>
public sealed class GetAudienceParticipantValidator : Validator<GetAudienceParticipantRequest>
{
    public GetAudienceParticipantValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.MemberId).GreaterThan(0);
    }
}

/// <summary>
/// Request to read an audience participant.
/// </summary>
public sealed class GetAudienceParticipantRequest
{
    public long FormId { get; init; }

    public long MemberId { get; init; }
}
