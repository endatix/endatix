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
/// Counts the real people a search keeps and how many of them a link batch would issue to.
/// </summary>
public sealed class Preview(IMediator mediator, ITenantContext tenantContext)
    : Endpoint<PreviewLinksRequest, Results<Ok<LinkPreviewResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("forms/{formId}/audience/links/preview");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Preview a Personalized link batch";
            summary.Description = "matching counts the real people whose identifier contains search (ignoring " +
                "case under the database's default collation; blank keeps everyone), withoutLink those of them with " +
                "no link yet, cap the most links one batch issues.";
            summary.Responses[200] = "Counts retrieved.";
            summary.Responses[400] = "Invalid search.";
            summary.Responses[404] = "Form not found.";
        });
        Description(builder => builder
            .Produces<LinkPreviewResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<LinkPreviewResponse>, ProblemHttpResult>> ExecuteAsync(
        PreviewLinksRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new PreviewLinksQuery(
                new LinkBatchScope(tenantContext.TenantId, request.FormId, new PeopleFilter(request.Search))),
            ct);
        return TypedResultsBuilder
            .MapResult(result, LinkPreviewResponse.From)
            .SetTypedResults<Ok<LinkPreviewResponse>, ProblemHttpResult>();
    }
}

/// <summary>Validates <see cref="PreviewLinksRequest"/>.</summary>
public sealed class PreviewLinksValidator : Validator<PreviewLinksRequest>
{
    /// <summary>The form id, and a search no longer than an identifier.</summary>
    public PreviewLinksValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.Search).ValidPeopleSearch();
    }
}

/// <summary>Request to preview a Personalized link batch.</summary>
public sealed class PreviewLinksRequest
{
    /// <summary>The form whose participants are counted.</summary>
    public long FormId { get; init; }

    /// <summary>
    /// Part of an identifier, matched ignoring case under a default collation; at most as long as an identifier.
    /// </summary>
    public string? Search { get; init; }
}

/// <summary>What a link batch with the same search would do.</summary>
public sealed class LinkPreviewResponse
{
    /// <summary>Real people the search keeps.</summary>
    public int Matching { get; init; }
    /// <summary>Of those, the people with no link yet: who a batch would issue to.</summary>
    public int WithoutLink { get; init; }
    /// <summary>The most links one batch issues.</summary>
    public int Cap { get; init; }

    /// <summary>Maps the handler result to the response.</summary>
    public static LinkPreviewResponse From(LinkPreviewDto preview) =>
        new() { Matching = preview.Matching, WithoutLink = preview.WithoutLink, Cap = preview.Cap };
}
