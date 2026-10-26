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

public sealed class PreviewLinksValidator : Validator<PreviewLinksRequest>
{
    public PreviewLinksValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.Search).ValidPeopleSearch();
    }
}

public sealed class PreviewLinksRequest
{
    public long FormId { get; init; }

    /// <summary>
    /// Part of an identifier, matched ignoring case under a default collation; at most as long as an identifier.
    /// </summary>
    public string? Search { get; init; }
}

public sealed class LinkPreviewResponse
{
    public int Matching { get; init; }
    public int WithoutLink { get; init; }
    public int Cap { get; init; }

    public static LinkPreviewResponse From(LinkPreviewDto preview) =>
        new() { Matching = preview.Matching, WithoutLink = preview.WithoutLink, Cap = preview.Cap };
}
