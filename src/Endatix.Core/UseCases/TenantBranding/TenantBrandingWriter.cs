using Endatix.Core.Abstractions;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.TenantBranding;

/// <summary>
/// The save every write of a tenant's branding shares: load the row (or start one), apply the change, and save only
/// when the revision moved, turning a lost race into a conflict.
/// </summary>
internal sealed class TenantBrandingWriter(IRepository<Entities.TenantBranding> repository, long tenantId)
{
    /// <summary>
    /// Why the tenant may not write its branding: no tenant (Unauthorized), or no <c>branding</c> entitlement
    /// (Forbidden). Null when it may.
    /// </summary>
    internal static async Task<Result?> RefusalAsync(
        long tenantId,
        ITenantEntitlements entitlements,
        CancellationToken cancellationToken)
    {
        if (tenantId <= 0)
        {
            return Result.Unauthorized();
        }

        return await entitlements.HasAsync(tenantId, Entitlements.BRANDING, cancellationToken)
            ? null
            : Result.Forbidden("The tenant's plan does not include branding.");
    }

    internal async Task<Result<TenantBrandingDto>> WriteAsync(
        Func<Entities.TenantBranding, Result> change,
        CancellationToken cancellationToken)
    {
        var stored = await repository.FirstOrDefaultAsync(new TenantBrandingByTenantIdSpec(tenantId), cancellationToken);
        var branding = stored ?? new Entities.TenantBranding(tenantId);
        var revision = branding.Revision;

        var result = change(branding);
        if (!result.IsSuccess)
        {
            return result;
        }

        var saved = branding.Revision == revision || await TrySaveAsync(branding, stored is null, cancellationToken);
        return saved
            ? TenantBrandingDto.From(branding)
            : Result.Conflict("The branding changed while it was being saved; read it again and retry.");
    }

    private async Task<bool> TrySaveAsync(Entities.TenantBranding branding, bool isNew, CancellationToken cancellationToken)
    {
        try
        {
            if (isNew)
            {
                await repository.AddAsync(branding, cancellationToken);
            }
            else
            {
                await repository.UpdateAsync(branding, cancellationToken);
            }

            return true;
        }
        catch (TenantBrandingConflictException)
        {
            return false;
        }
    }
}
