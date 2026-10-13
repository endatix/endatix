using Endatix.Api.Infrastructure;
using Endatix.Modules.Audience.Features.Links;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.Links;

public sealed class Redeem(IMediator mediator)
    : Endpoint<RedeemLinkRequest, Results<Ok<RedeemedLinkResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("forms/{formId}/audience/links/{token}/redeem");
        AllowAnonymous();
        // In-process only. The host rewrites X-Forwarded-For when the peer is a known proxy.
        Throttle(60, 60);
        Summary(summary =>
        {
            summary.Summary = "Open a personalised link";
            summary.Responses[200] = "Submission opened or resumed.";
            summary.Responses[400] = "Invalid input.";
            summary.Responses[404] = "Link not found.";
            summary.Responses[403] = "Too many requests.";
            summary.Responses[503] = "Another open of the same link is still running. Try again.";
        });
        Description(builder => builder
            .Produces<RedeemedLinkResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(503));
    }

    public override async Task<Results<Ok<RedeemedLinkResponse>, ProblemHttpResult>> ExecuteAsync(
        RedeemLinkRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new RedeemLinkCommand(request.FormId, request.Token), ct);
        return TypedResultsBuilder
            .MapResult(result, RedeemedLinkResponse.From)
            .SetTypedResults<Ok<RedeemedLinkResponse>, ProblemHttpResult>();
    }
}

public sealed class RedeemLinkValidator : Validator<RedeemLinkRequest>
{
    public RedeemLinkValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.Token).NotEmpty();
    }
}

public sealed class RedeemLinkRequest
{
    public long FormId { get; init; }
    public string Token { get; init; } = string.Empty;
}

/// <summary>
/// What an anonymous open needs: the submission and a token to work on it. The frozen snapshot
/// stays internal; clients read the typed <c>personalization</c> on the submission instead.
/// </summary>
public sealed class RedeemedLinkResponse
{
    public long SubmissionId { get; init; }
    public bool Created { get; init; }
    public string AccessToken { get; init; } = string.Empty;

    public static RedeemedLinkResponse From(RedeemedLinkDto dto) => new()
    {
        SubmissionId = dto.SubmissionId,
        Created = dto.Created,
        AccessToken = dto.AccessToken,
    };
}
