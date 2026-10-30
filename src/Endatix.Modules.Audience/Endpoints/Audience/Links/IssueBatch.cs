using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Audience.Endpoints.Audience.People;
using Endatix.Modules.Audience.Features.Links;
using Endatix.Modules.Audience.Features.People;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.Links;

/// <summary>
/// Issues Personalized links to the real people a filter keeps who have none yet.
/// </summary>
public sealed class IssueBatch(IMediator mediator, ITenantContext tenantContext)
    : Endpoint<IssueLinkBatchRequest, Results<Ok<IssuedLinkBatchResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Post("forms/{formId}/audience/links/batches");
        Permissions(Actions.Forms.Edit);
        Summary(Describe);
        Description(builder => builder
            .Produces<IssuedLinkBatchResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(503));
    }

    private static void Describe(EndpointSummary summary)
    {
        summary.Summary = "Issue a Personalized link batch";
        summary.Description = "Issues a link to each real person whose identifier contains filter.search " +
            "(ignoring case under the database's default collation; blank or no filter keeps everyone) and who has " +
            "none. matching counts the real people the filter keeps; issued has only the new links, each raw token " +
            "returned once.";
        summary.Responses[200] = "Links issued.";
        summary.Responses[400] = "Invalid search, or more than 5,000 people would get a link.";
        summary.Responses[404] = "Form not found.";
        summary.Responses[503] = "Other link batches on this form kept issuing to the same people. Try again.";
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<IssuedLinkBatchResponse>, ProblemHttpResult>> ExecuteAsync(
        IssueLinkBatchRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new IssueLinksCommand(
                new LinkBatchScope(tenantContext.TenantId, request.FormId, new PeopleFilter(request.Filter?.Search))),
            ct);
        return TypedResultsBuilder
            .MapResult(result, IssuedLinkBatchResponse.From)
            .SetTypedResults<Ok<IssuedLinkBatchResponse>, ProblemHttpResult>();
    }
}

/// <summary>Validates <see cref="IssueLinkBatchRequest"/>.</summary>
public sealed class IssueLinkBatchValidator : Validator<IssueLinkBatchRequest>
{
    /// <summary>The form id, and a search no longer than an identifier.</summary>
    public IssueLinkBatchValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.Filter)
            .ChildRules(filter => filter.RuleFor(item => item!.Search).ValidPeopleSearch());
    }
}

/// <summary>Request to issue a Personalized link batch.</summary>
public sealed class IssueLinkBatchRequest
{
    /// <summary>The form whose participants get links.</summary>
    public long FormId { get; init; }

    /// <summary>Which real people get a link; absent keeps everyone.</summary>
    public LinkBatchFilterRequest? Filter { get; init; }
}

/// <summary>Which real people a link batch is for.</summary>
public sealed class LinkBatchFilterRequest
{
    /// <summary>
    /// Part of an identifier, matched ignoring case under a default collation; at most as long as an identifier.
    /// </summary>
    public string? Search { get; init; }
}

/// <summary>The links one batch issued.</summary>
public sealed class IssuedLinkBatchResponse
{
    /// <summary>Real people the filter keeps, with or without a link.</summary>
    public int Matching { get; init; }
    /// <summary>The new links only; a person who already had one is left out.</summary>
    public IReadOnlyList<IssuedPersonLinkResponse> Issued { get; init; } = [];

    /// <summary>Maps the handler result to the response.</summary>
    public static IssuedLinkBatchResponse From(IssuedLinkBatchDto batch) =>
        new()
        {
            Matching = batch.Matching,
            Issued = batch.Issued
                .Select(link => new IssuedPersonLinkResponse
                {
                    MembershipId = link.MembershipId,
                    Identifier = link.Identifier,
                    Token = link.Token,
                })
                .ToList(),
        };
}

/// <summary>One new link, with the participant it is for.</summary>
public sealed class IssuedPersonLinkResponse
{
    /// <summary>The participant's membership on the form.</summary>
    public long MembershipId { get; init; }
    /// <summary>The participant's identifier, as entered.</summary>
    public string Identifier { get; init; } = string.Empty;
    /// <summary>The raw link token, returned only here.</summary>
    public string Token { get; init; } = string.Empty;
}
