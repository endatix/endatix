using System.Diagnostics.CodeAnalysis;
using Endatix.Api.Common;
using Endatix.Api.Common.FeatureFlags;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.TenantBranding.History;
using Endatix.Framework.FeatureFlags;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.TenantBranding;

/// <summary>
/// Lists the current tenant's branding versions, newest first, so an admin can pick one to restore.
/// </summary>
[Experimental("ENDATIX0002")]
public sealed class ListTenantBrandingVersions(IMediator mediator)
    : Endpoint<ListTenantBrandingVersionsRequest, Results<Ok<Paged<TenantBrandingVersionModel>>, ProblemHttpResult>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Get("tenant-branding/versions");
        Permissions(Actions.Tenant.ManageSettings);
        FeatureFlag<EndpointFeatureGate>(FeatureFlags.WhiteLabeling);
        Summary(s =>
        {
            s.Summary = "List the tenant's branding versions";
            s.Description = "A paged list (items) of the tenant's branding versions, newest first: number, createdAt, " +
                "createdBy, source (publish, restore or reset) and restoredFromNumber; ids and numbers are strings. Empty, not " +
                "403, when the tenant's plan does not include branding, as every read shows no brand then.";
            s.Responses[200] = "The versions.";
            s.Responses[400] = $"Page below 1, or page size outside 1..{PagedRequestLimits.MAX_PAGE_SIZE}.";
            s.Responses[404] = "White-labeling is not enabled on this deployment.";
        });
        Description(builder => builder
            .Produces<Paged<TenantBrandingVersionModel>>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<Paged<TenantBrandingVersionModel>>, ProblemHttpResult>> ExecuteAsync(
        ListTenantBrandingVersionsRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new ListTenantBrandingVersionsQuery(request.ResolvePage(), request.ResolvePageSize()), ct);
        return result.IsSuccess
            ? TypedResults.Ok(result.Value.MapToPaged(TenantBrandingVersionModel.Map))
            : result.ToProblem();
    }
}

/// <summary>Request to list the tenant's branding versions.</summary>
[Experimental("ENDATIX0002")]
public sealed class ListTenantBrandingVersionsRequest : IPagedRequest
{
    /// <summary>The page, from 1; the first page when omitted. A page past the last one is the last page.</summary>
    public int? Page { get; set; }

    /// <summary>The number of versions per page, 1 to <see cref="PagedRequestLimits.MAX_PAGE_SIZE"/>; the default when omitted.</summary>
    public int? PageSize { get; set; }
}

/// <summary>Validator for <see cref="ListTenantBrandingVersionsRequest"/>: the shared paging rules.</summary>
[Experimental("ENDATIX0002")]
public sealed class ListTenantBrandingVersionsValidator : Validator<ListTenantBrandingVersionsRequest>
{
    /// <summary>A page below 1, or a page size outside 1 to the shared maximum, is 400.</summary>
    public ListTenantBrandingVersionsValidator()
    {
        Include(new PageableRequestValidator());

        RuleFor(x => x.PageSize)
            .LessThanOrEqualTo(PagedRequestLimits.MAX_PAGE_SIZE)
            .When(x => x.PageSize.HasValue);
    }
}
