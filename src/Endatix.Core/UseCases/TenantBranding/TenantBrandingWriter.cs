using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Branding;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Versioning;

namespace Endatix.Core.UseCases.TenantBranding;

/// <summary>
/// The save every write of a tenant's branding shares: load the row (or start one), apply the change, and save the new
/// version with it, turning a lost race into a conflict. A change that adds no version saves nothing.
/// </summary>
internal sealed class TenantBrandingWriter(ITenantBrandingRepository repository, long tenantId, ContentVersionStamp stamp)
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

        return await entitlements.IsEntitledAsync(tenantId, EntitlementNames.BRANDING, cancellationToken)
            ? null
            : Result.Forbidden("The tenant's plan does not include branding.");
    }

    /// <summary>Publishes <paramref name="value"/> as a new version.</summary>
    internal Task<Result<TenantBrandingDto>> PublishAsync(BrandOverride value, long expectedVersion, CancellationToken cancellationToken) =>
        WriteAsync(branding => Task.FromResult(branding.Publish(value, expectedVersion, stamp)), cancellationToken);

    /// <summary>Publishes version <paramref name="number"/> again, or for a rollback (null) the version before the live one.</summary>
    internal Task<Result<TenantBrandingDto>> RestoreAsync(long? number, long expectedVersion, CancellationToken cancellationToken) =>
        WriteAsync(async branding =>
        {
            var target = branding.RestoreTarget(number, expectedVersion);
            if (!target.IsSuccess)
            {
                return target.ToErrorResult<TenantBrandingVersion?>();
            }

            var source = await repository.FindVersionAsync(tenantId, target.Value, cancellationToken);
            return source is null
                ? Result.Conflict("The branding changed while it was being read; read it again and retry.")
                : branding.Restore(source, expectedVersion, stamp);
        }, cancellationToken);

    private async Task<Result<TenantBrandingDto>> WriteAsync(
        Func<Entities.TenantBranding, Task<Result<TenantBrandingVersion?>>> change,
        CancellationToken cancellationToken)
    {
        var branding = await repository.FindAsync(tenantId, cancellationToken) ?? new Entities.TenantBranding(tenantId);
        var result = await change(branding);
        if (!result.IsSuccess)
        {
            return result.ToErrorResult<TenantBrandingDto>();
        }

        var saved = result.Value is not { } version || await repository.SaveOrConcedeAsync(branding, version, cancellationToken);
        return saved
            ? TenantBrandingDto.From(branding)
            : Result.Conflict("The branding changed while it was being saved; read it again and retry.");
    }
}
