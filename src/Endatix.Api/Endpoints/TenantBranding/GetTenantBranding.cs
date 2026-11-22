using System.Diagnostics.CodeAnalysis;
using Endatix.Api.Common.FeatureFlags;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.TenantBranding.Get;
using Endatix.Framework.FeatureFlags;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.TenantBranding;

/// <summary>
/// Reads the current tenant's branding: whether its plan includes branding, the live brand, and the version to write
/// from. Any signed-in Hub user of the tenant may read it, since the Hub shows the brand to all of them; only writes
/// need <c>tenant.settings.manage</c>.
/// </summary>
[Experimental("ENDATIX0002")]
public sealed class GetTenantBranding(IMediator mediator)
    : EndpointWithoutRequest<Results<Ok<TenantBrandingModel>, ProblemHttpResult>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Get("tenant-branding");
        Permissions(Actions.Access.Hub);
        FeatureFlag<EndpointFeatureGate>(FeatureFlags.WhiteLabeling);
        Summary(s =>
        {
            s.Summary = "Get the tenant's branding";
            s.Description = "Returns entitled, the published brand override as an object (null when the deployment brand " +
                "applies or the tenant is not entitled), its number, and the version. The ETag header holds the version " +
                "a write names in If-Match: \"0\" before the first publish.";
            s.Responses[200] = "The tenant's branding.";
            s.Responses[404] = "White-labeling is not enabled on this deployment.";
        });
        Description(builder => builder
            .Produces<TenantBrandingModel>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<TenantBrandingModel>, ProblemHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        var result = await mediator.Send(new GetTenantBrandingQuery(), ct);
        if (!result.IsSuccess)
        {
            return TenantBrandingHttp.ToProblem(result);
        }

        TenantBrandingHttp.SetETag(HttpContext, result.Value.Branding.Version);
        return TypedResults.Ok(TenantBrandingModel.Map(result.Value));
    }
}
