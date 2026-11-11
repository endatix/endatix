using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication.Providers;

namespace Endatix.Infrastructure.Multitenancy;

/// <summary>
/// The outcome of <see cref="TenantContextInitializer.TryInitialize"/>.
/// </summary>
public enum TenantInitializationResult
{
    /// <summary>The tenant was resolved and set on the tenant context.</summary>
    Set,

    /// <summary>The signed-in user has no tenant greater than zero.</summary>
    NoTenant,

    /// <summary>The tenant context is not the writable <see cref="TenantContext"/>.</summary>
    NotWritable,
}

/// <summary>
/// Resolves the tenant of a signed-in user and sets it on the request's tenant context.
/// </summary>
/// <remarks>
/// The tenant comes from the token's own <c>tid</c> claim when Endatix issued the token (user JWT or ReBAC
/// form-access JWT), and from the hydrated <see cref="AuthorizedIdentity"/> for every other issuer.
/// </remarks>
public static class TenantContextInitializer
{
    /// <summary>
    /// Sets the tenant of <paramref name="user"/> on <paramref name="tenantContext"/>.
    /// </summary>
    /// <param name="tenantContext">The request's tenant context.</param>
    /// <param name="user">The signed-in user.</param>
    /// <param name="jwtOptions">The Endatix JWT options naming the issuers whose <c>tid</c> claim is read directly.</param>
    /// <returns>Whether the tenant was set, could not be resolved, or the tenant context is not writable.</returns>
    public static TenantInitializationResult TryInitialize(
        ITenantContext tenantContext,
        ClaimsPrincipal user,
        EndatixJwtOptions jwtOptions)
    {
        if (tenantContext is not TenantContext writableTenantContext)
        {
            return TenantInitializationResult.NotWritable;
        }

        var tenantId = ResolveTenantId(user, jwtOptions);
        if (tenantId <= 0)
        {
            return TenantInitializationResult.NoTenant;
        }

        writableTenantContext.SetTenant(tenantId);
        return TenantInitializationResult.Set;
    }

    private static long ResolveTenantId(ClaimsPrincipal user, EndatixJwtOptions jwtOptions)
    {
        var issuer = user.GetIssuer();
        if (issuer is not null && (issuer == jwtOptions.Issuer || issuer == jwtOptions.ReBacIssuer))
        {
            var tenantClaim = (user.Identity as ClaimsIdentity)?.FindFirst(ClaimNames.TenantId);
            return long.TryParse(tenantClaim?.Value, out var tokenTenantId) ? tokenTenantId : 0;
        }

        return user.Identities.OfType<AuthorizedIdentity>().FirstOrDefault()?.TenantId ?? 0;
    }
}
