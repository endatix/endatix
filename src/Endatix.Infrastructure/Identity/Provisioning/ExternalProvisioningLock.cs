using Microsoft.Extensions.Caching.Memory;

namespace Endatix.Infrastructure.Identity.Provisioning;

/// <summary>
/// Best-effort, single-process lock for first-time external AppUser JIT provisioning.
/// </summary>
/// <remarks>
/// Uses an in-memory <see cref="MemoryCache"/> and <see cref="SemaphoreSlim"/> per
/// (tenantId, authProvider, externalSubjectId), so it only serializes concurrent provisioning
/// within one app instance. Multi-pod deployments can still race on the same key across nodes.
/// DB unique constraints and <see cref="ExternalAppUserProvisioner"/> recovery paths are the
/// authority under scale-out; this lock mainly reduces duplicate-key exceptions on a single node.
/// A distributed lock would only be warranted if first-login contention becomes noisy in production.
/// </remarks>
internal static class ExternalProvisioningLock
{
    private static readonly TimeSpan _slidingExpiration = TimeSpan.FromMinutes(30);
    private static readonly MemoryCache _cache = new(new MemoryCacheOptions());

    /// <summary>
    /// Gets the process-local semaphore for the given external identity key.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="authProvider">The authentication provider.</param>
    /// <param name="externalSubjectId">The external subject ID.</param>
    /// <returns>The process-local semaphore for the given external identity key.</returns>
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
