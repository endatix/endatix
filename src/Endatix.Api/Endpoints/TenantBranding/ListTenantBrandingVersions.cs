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
                "createdBy, source (publish or restore) and restoredFromNumber; ids and numbers are strings. Empty, not " +
                "403, when the tenant's plan does not include branding, as every read shows no brand then.";
            s.Responses[200] = "The versions.";
            s.Responses[400] = "Invalid page or page size.";
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
        var result = await mediator.Send(new ListTenantBrandingVersionsQuery(request.Page, request.PageSize), ct);
        return result.IsSuccess
            ? TypedResults.Ok(result.Value.MapToPaged(TenantBrandingVersionModel.Map))
            : result.ToProblem();
    }
}

/// <summary>Request to list the tenant's branding versions.</summary>
[Experimental("ENDATIX0002")]
public sealed class ListTenantBrandingVersionsRequest : IPagedRequest
{
    /// <summary>The page, from 1; out-of-range values are clamped, as every paged list does.</summary>
    public int? Page { get; set; }

    /// <summary>The page, from 1; out-of-range values are clamped, as every paged list does.</summary>
    public int? PageSize { get; set; }
}
