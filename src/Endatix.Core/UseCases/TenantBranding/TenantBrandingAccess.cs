using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.TenantBranding;

/// <summary>Checks tenant scope and entitlement for branding writes.</summary>
[Experimental("ENDATIX0006")]
public sealed class TenantBrandingAccess(ITenantContext tenant, ITenantEntitlements entitlements)
{
    internal long TenantId => tenant.TenantId;

    internal async Task<Result<TenantBrandingDto>?> WriteRefusalAsync(CancellationToken ct)
    {
        if (TenantId <= 0)
        {
            return Result<TenantBrandingDto>.Unauthorized();
        }

        return await entitlements.IsEntitledAsync(TenantId, EntitlementNames.BRANDING, ct)
            ? null
            : Result<TenantBrandingDto>.Forbidden("The tenant's plan does not include branding.");
    }
}
