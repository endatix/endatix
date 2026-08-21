using Ardalis.Specification;
using Endatix.Core.Common;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>
/// Specifications for platform-scoped Tenant reads and writes.
/// </summary>
/// <remarks>
/// The acting PlatformAdmin carries their own tenant context, so these specifications ignore the global
/// tenant filter that would otherwise hide the target tenant's settings row (<see cref="TenantSettings"/>
/// is tenant-owned).
/// </remarks>
public static class TenantSpecifications
{
    /// <summary>
    /// Matches any tenant holding the given short URL, including soft-deleted rows (unique index is unfiltered).
    /// </summary>
    public sealed class ExistsByShortUrlSpec : Specification<Tenant>
    {
        public ExistsByShortUrlSpec(string shortUrl)
        {
            var normalized = ShortUrl.Normalize(shortUrl);
            Query
                .IgnoreQueryFilters()
                .Where(tenant => tenant.ShortUrl == normalized);
        }
    }

    /// <summary>
    /// Loads a live tenant for platform-scoped edits.
    /// </summary>
    public sealed class ByIdSpec : Specification<Tenant>, ISingleResultSpecification<Tenant>
    {
        public ByIdSpec(long tenantId)
        {
            Query
                .IgnoreQueryFilters()
                .Where(tenant => tenant.Id == tenantId && !tenant.IsDeleted);
        }
    }

    /// <summary>
    /// Loads live tenants matching the given ids. Used for the current user's membership list.
    /// </summary>
    public sealed class ByIdsSpec : Specification<Tenant>
    {
        public ByIdsSpec(IReadOnlyCollection<long> tenantIds)
        {
            Query
                .IgnoreQueryFilters()
                .Where(tenant => tenantIds.Contains(tenant.Id) && !tenant.IsDeleted);
        }
    }

    /// <summary>
    /// Loads settings for platform-scoped edits (tracked).
    /// </summary>
    public sealed class SettingsByTenantIdSpec : Specification<TenantSettings>, ISingleResultSpecification<TenantSettings>
    {
        public SettingsByTenantIdSpec(long tenantId)
        {
            Query
                .IgnoreQueryFilters()
                .Where(settings => settings.TenantId == tenantId);
        }
    }
}
