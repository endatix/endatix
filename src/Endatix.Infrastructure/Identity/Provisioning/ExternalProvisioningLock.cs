using Microsoft.Extensions.Caching.Memory;

namespace Endatix.Infrastructure.Identity.Provisioning;

/// <summary>
/// Provides a lock for external operator provisioning.
/// </summary>
internal static class ExternalProvisioningLock
{
    private static readonly TimeSpan _slidingExpiration = TimeSpan.FromMinutes(30);
    private static readonly MemoryCache _cache = new(new MemoryCacheOptions());

    /// <summary>
    /// Gets a lock for external operator provisioning.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="authProvider">The authentication provider.</param>
    /// <param name="externalSubjectId">The external subject ID.</param>
    /// <returns>A lock for external operator provisioning.</returns>
    public static SemaphoreSlim Get(long tenantId, string authProvider, string externalSubjectId)
    {
        var key = $"{tenantId}:{authProvider}:{externalSubjectId}";
        return _cache.GetOrCreate(key, CreateEntry)!;
    }

    private static SemaphoreSlim CreateEntry(ICacheEntry entry)
    {
        entry.SlidingExpiration = _slidingExpiration;
        entry.RegisterPostEvictionCallback(static (_, value, _, _) =>
        {
            if (value is SemaphoreSlim semaphore)
            {
                semaphore.Dispose();
            }
        });

        return new SemaphoreSlim(1, 1);
    }
}
