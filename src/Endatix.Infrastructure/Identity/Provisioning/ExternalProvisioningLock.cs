using System.Collections.Concurrent;

namespace Endatix.Infrastructure.Identity.Provisioning;

/// <summary>
/// Provides a lock for external operator provisioning.
/// </summary>
internal static class ExternalProvisioningLock
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

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
        return _locks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
    }
}
