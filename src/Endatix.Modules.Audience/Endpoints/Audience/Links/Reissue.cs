using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Audience.Features.Links;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.Links;

public sealed class Reissue(IMediator mediator, ITenantContext tenantContext)
    : Endpoint<ReissueLinkRequest, Results<Ok<IssuedLinkResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("forms/{formId}/audience/people/{membershipId}/link/reissue");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Reissue one person's personalised link";
            summary.Description =
                "Gives the link a new token, returned once. The old token no longer opens it, and access tokens from " +
                "earlier opens are refused within a minute. An opened link keeps its submission.";
            summary.Responses[200] = "Link reissued.";
            summary.Responses[400] = "Invalid input, or the person is a test participant.";
            summary.Responses[404] = "Form or person not found, or the person has no link yet.";
        });
        Description(builder => builder
            .Produces<IssuedLinkResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    public override async Task<Results<Ok<IssuedLinkResponse>, ProblemHttpResult>> ExecuteAsync(
        ReissueLinkRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new ReissueLinkCommand(tenantContext.TenantId, request.FormId, request.MembershipId), ct);
        return TypedResultsBuilder
            .MapResult(result, IssuedLinkResponse.From)
            .SetTypedResults<Ok<IssuedLinkResponse>, ProblemHttpResult>();
    }
}

public sealed class ReissueLinkValidator : Validator<ReissueLinkRequest>
{
    public ReissueLinkValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.MembershipId).GreaterThan(0);
    }
}

public sealed class ReissueLinkRequest
{
    public long FormId { get; init; }

    public long MembershipId { get; init; }
}
