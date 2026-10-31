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
/// Reads one participant on this form, by audience member id, with their submissions on it.
/// </summary>
public sealed class Get(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<GetAudienceParticipantRequest, Results<Ok<AudienceParticipantResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("forms/{formId}/audience/people/{memberId}");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Get audience participant";
            summary.Description =
                "Reads one participant on this form by audience member id: values, Link status, and the newest 50 of their " +
                "submissions on the form with the total.";
            summary.Responses[200] = "The participant.";
            summary.Responses[404] = "Form not found, or the participant is not on this form.";
        });
        Description(builder => builder
            .Produces<AudienceParticipantResponse>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    /// <inheritdoc />
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
    /// <summary>Both ids are positive.</summary>
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
    /// <summary>The form.</summary>
    public long FormId { get; init; }

    /// <summary>The audience member id (<c>audienceMemberId</c> on the list), not the membership id.</summary>
    public long MemberId { get; init; }
}
