using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.TenantBranding.Get;

/// <summary>
/// Reads the current tenant's branding. A tenant without the <c>branding</c> entitlement reads no brand, but still
/// reads the version, so the row's state stays visible to support.
/// </summary>
internal sealed class GetTenantBrandingHandler(
    ITenantBrandingRepository repository,
    ITenantContext tenantContext,
    ITenantEntitlements entitlements)
    : IQueryHandler<GetTenantBrandingQuery, Result<TenantBrandingReadDto>>
{
    /// <inheritdoc/>
    public async Task<Result<TenantBrandingReadDto>> Handle(GetTenantBrandingQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        if (tenantId <= 0)
        {
            return Result.Unauthorized();
        }

        var entitled = await entitlements.IsEntitledAsync(tenantId, EntitlementNames.BRANDING, cancellationToken);
        var branding = await repository.FindAsync(tenantId, cancellationToken);
        var version = branding?.Version ?? Entities.TenantBranding.UNSAVED_VERSION;
        var dto = entitled && branding is not null
            ? TenantBrandingDto.From(branding)
            : new TenantBrandingDto(null, null, version, null);
        return new TenantBrandingReadDto(entitled, dto);
    }
}
