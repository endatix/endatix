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
        Summary(Describe);
        Description(builder => builder
            .Produces<RedeemedLinkResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(503));
    }

    private static void Describe(EndpointSummary summary)
    {
        summary.Summary = "Open a personalised link";
        summary.Responses[200] = "Submission opened or resumed.";
        summary.Responses[400] = "Invalid input.";
        summary.Responses[404] = "Link not found.";
        summary.Responses[409] = "The link's submission has ended (errorCode submission_ended, submissionId).";
        summary.Responses[403] = "Too many requests.";
        summary.Responses[503] = "Another open of the same link is still running. Try again.";
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
/// What an anonymous open needs: the submission and a token to work on it. The frozen snapshot stays internal;
/// clients read the typed <c>personalization</c> on the submission instead.
/// </summary>
/// <param name="SubmissionId">The opened or resumed submission.</param>
/// <param name="Created">Whether this open started it.</param>
/// <param name="AccessToken">
/// The submission's opaque token, a <c>SubmissionToken</c>: it follows the form's privacy, so it opens nothing on a
/// private form without a session.
/// </param>
/// <param name="SubmissionAccessToken">
/// A signed access token with <c>submit</c>, sent as <c>tokenType=AccessToken</c>: it fills in this submission on a
/// public or private form, and stops once the link is reissued or the person leaves the form.
/// </param>
/// <param name="SubmissionAccessTokenExpiresAt">When <paramref name="SubmissionAccessToken"/> stops working, in UTC.</param>
public sealed record RedeemedLinkResponse(
    long SubmissionId,
    bool Created,
#pragma warning disable S1133 // Intentional: AccessToken stays until a later release removes it.
    [property: Obsolete("Use SubmissionAccessToken. AccessToken is removed in a later release.")] string AccessToken,
#pragma warning restore S1133
    string SubmissionAccessToken,
    DateTime SubmissionAccessTokenExpiresAt)
{
    /// <summary>The response for an opened link.</summary>
    public static RedeemedLinkResponse From(RedeemedLinkDto dto) =>
        new(dto.SubmissionId, dto.Created, dto.AccessToken, dto.SubmissionAccessToken, dto.SubmissionAccessTokenExpiresAt);
}
