using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Attributes;
using Endatix.Infrastructure.Identity.Authorization;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticAssets;

namespace Endatix.Api.Infrastructure;

/// <summary>Applies account standing after authorization in an endpoint-routing pipeline.</summary>
[Experimental("ENDATIX0006")]
public static class RequiredActionsGateExtensions
{
    /// <summary>Installs the standing check for the matched endpoint, before it executes.</summary>
    public static IApplicationBuilder UseRequiredActionsGate(this IApplicationBuilder app) =>
        app.UseMiddleware<RequiredActionsGate>();
}

internal sealed class RequiredActionsGate(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AccountStanding standing)
    {
        if (standing.IsEnforced && context.User.Identities.Any(identity => identity.IsAuthenticated) &&
            context.GetEndpoint() is { } endpoint && AppliesTo(endpoint.Metadata) &&
            await standing.RefusalAsync(context.RequestAborted) is { } refusal)
        {
            await refusal.ToProblem().ExecuteAsync(context);
            return;
        }

        await next(context);
    }

    private static bool AppliesTo(EndpointMetadataCollection metadata)
    {
        var definition = metadata.GetMetadata<EndpointDefinition>();
        return metadata.GetMetadata<IAllowAnonymous>() is null &&
            metadata.GetMetadata<StaticAssetDescriptor>() is null &&
            metadata.GetMetadata<AllowWithRequiredActionsAttribute>() is null &&
            definition?.EndpointAttributes?.OfType<AllowWithRequiredActionsAttribute>().Any() != true &&
            !IsRespondent(metadata, definition);
    }

    private static bool IsRespondent(EndpointMetadataCollection metadata, EndpointDefinition? definition)
    {
        if (metadata.GetMetadata<AuthorizationPolicy>() is not null || metadata.GetMetadata<IAuthorizationRequirementData>() is not null)
        {
            return false;
        }
        var policies = metadata.GetOrderedMetadata<IAuthorizeData>();
        // FastEndpoints adds a generated policy even when the only declared policy is a form token.
        var companion = definition is
        {
            AllowedPermissions: not { Count: > 0 }, AllowedRoles: not { Count: > 0 },
            AllowedClaimTypes: not { Count: > 0 }, AllowedScopes: not { Count: > 0 }
        } ? definition.SecurityPolicyName : null;
        return policies.Any(p => p.Policy == AuthorizationPolicies.PublicResourceAccess)
            && policies.All(p => (p.Policy == AuthorizationPolicies.PublicResourceAccess ||
                (companion is not null && p.Policy == companion)) &&
                string.IsNullOrEmpty(p.Roles) && string.IsNullOrEmpty(p.AuthenticationSchemes));
    }
}
