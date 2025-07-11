using System.Security.Claims;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Transforms the claims of a JWT token to the claims of a user.
/// </summary>
public sealed class JwtClaimsTransformer(IConfiguration configuration) : IClaimsTransformation
{
    private const long DEFAULT_TENANT_ID = 1;

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var issuer = principal.FindFirstValue(JwtRegisteredClaimNames.Iss);

        if (issuer == null)
        {
            return Task.FromResult(principal);
        }

        var permissionClaim = principal.FindFirstValue(ClaimNames.Permission);

        if (permissionClaim == Allow.AllowAll)
        {
            return Task.FromResult(principal);
        }
        ;


        if (principal.Identity is not ClaimsIdentity primaryIdentity)
        {
            return Task.FromResult(principal);
        }

        // Add the permission claim to the principal to allow access to all resources
        // This is to achieve the same effect as the [Premission(Allow.AllowAll)] auth filter
        // TODO: Remove this with the RBAC implementation
        primaryIdentity.AddClaim(new Claim(ClaimNames.Permission, Allow.AllowAll));

        var tenantIdClaim = principal.FindFirstValue(ClaimNames.TenantId);
        if (tenantIdClaim == null)
        {
            primaryIdentity.AddClaim(new Claim(ClaimNames.TenantId, $"{DEFAULT_TENANT_ID}"));
        }

        return Task.FromResult(principal);
    }
}
