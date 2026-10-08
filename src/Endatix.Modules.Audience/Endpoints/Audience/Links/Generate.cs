using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Common;
using Endatix.Modules.Audience.Features.Links;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.Links;

public sealed class Generate(IMediator mediator, ITenantContext tenantContext)
    : Endpoint<GenerateLinksRequest, Results<Ok<IReadOnlyList<IssuedLinkResponse>>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("forms/{formId}/audience/links");
        Permissions(Actions.Forms.Edit);
        Summary(summary => summary.Summary = "Issue personalised links");
    }

    public override async Task<Results<Ok<IReadOnlyList<IssuedLinkResponse>>, ProblemHttpResult>> ExecuteAsync(
        GenerateLinksRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GenerateLinksCommand(tenantContext.TenantId, request.FormId), ct);
        return TypedResultsBuilder
            .MapResult(result, IssuedLinkResponse.From)
            .SetTypedResults<Ok<IReadOnlyList<IssuedLinkResponse>>, ProblemHttpResult>();
    }
}

public sealed class GenerateLinksValidator : Validator<GenerateLinksRequest>
{
    public GenerateLinksValidator() => RuleFor(request => request.FormId).GreaterThan(0);
}

public sealed class GenerateLinksRequest
{
    public long FormId { get; init; }
}

public sealed class IssuedLinkResponse
{
    public long MembershipId { get; init; }
    public string Token { get; init; } = string.Empty;

    public static IReadOnlyList<IssuedLinkResponse> From(IReadOnlyList<IssuedLinkDto> issued) =>
        issued.Select(link => new IssuedLinkResponse { MembershipId = link.MembershipId, Token = link.Token }).ToList();
}
