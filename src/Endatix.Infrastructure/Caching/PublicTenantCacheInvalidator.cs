using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Caching;

/// <summary>
/// The public read is cached by the tenant's short URL, which a tenant-scoped write does not hold, so this looks it up
/// from the scope's tenant.
/// </summary>
internal sealed class PublicTenantCacheInvalidator(
    ITenantContext tenantContext,
    IRepository<Tenant> tenantRepository,
    HybridCache hybridCache,
    ILogger<PublicTenantCacheInvalidator> logger) : IPublicTenantCacheInvalidator
{
    public async Task InvalidateCurrentTenantAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        try
        {
            var tenant = await tenantRepository.SingleOrDefaultAsync(new TenantSpecifications.ByIdSpec(tenantId), cancellationToken);
            if (tenant is not null)
            {
                await hybridCache.RemoveAsync(PublicTenantCacheKeys.Entry(tenant.ShortUrl), cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to evict the public read of tenant {TenantId}. It may be stale until it expires.", tenantId);
        }
    }
}
