using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.TenantBranding;

/// <summary>Account access to the current tenant's brand. Only writes require the branding entitlement.</summary>
[Experimental("ENDATIX0006")]
public sealed class TenantBrandingAccess(ITenantContext tenant, ITenantEntitlements entitlements, AccountStanding standing)
{
    internal long TenantId => tenant.TenantId;

    /// <summary>Checks a signed-in read before any brand or history is fetched.</summary>
    public Task<Result?> ReadRefusalAsync(CancellationToken ct) => TenantId > 0
        ? standing.RefusalAsync(ct)
        : Task.FromResult<Result?>(Result.Unauthorized());

    internal async Task<Result<TenantBrandingDto>?> WriteRefusalAsync(CancellationToken ct)
    {
        if (TenantId <= 0)
        {
            return Result<TenantBrandingDto>.Unauthorized();
        }

        return await entitlements.IsEntitledAsync(TenantId, EntitlementNames.BRANDING, ct)
            ? await standing.RefusalAsync<TenantBrandingDto>(ct)
            : Result<TenantBrandingDto>.Forbidden("The tenant's plan does not include branding.");
    }
}
