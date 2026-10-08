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
        Summary(summary => summary.Summary = "Open a personalised link");
    }

    public override async Task<Results<Ok<RedeemedLinkResponse>, ProblemHttpResult>> ExecuteAsync(
        RedeemLinkRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new RedeemLinkCommand(request.FormId, request.Token!), ct);
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
    public string? Token { get; init; }
}

public sealed class RedeemedLinkResponse
{
    public long SubmissionId { get; init; }
    public string Snapshot { get; init; } = "{}";
    public bool Created { get; init; }
    public string AccessToken { get; init; } = string.Empty;

    public static RedeemedLinkResponse From(RedeemedLinkDto dto) => new()
    {
        SubmissionId = dto.SubmissionId,
        Snapshot = dto.Snapshot,
        Created = dto.Created,
        AccessToken = dto.AccessToken,
    };
}
