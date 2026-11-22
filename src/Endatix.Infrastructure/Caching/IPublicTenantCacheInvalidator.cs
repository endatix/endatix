using System.Diagnostics.CodeAnalysis;

namespace Endatix.Infrastructure.Caching;

/// <summary>
/// Evicts a tenant's cached public read (<c>GET public/tenants/{slug}</c>) after a write that changes what it returns.
/// </summary>
[Experimental("ENDATIX0002")]
public interface IPublicTenantCacheInvalidator
{
    /// <summary>
    /// Removes the current tenant's public read from this node's cache and the shared one. A failure is logged, not thrown: the
    /// write it follows has already been saved, and the entry expires within <see cref="PublicTenantCacheKeys.Ttl"/>.
    /// </summary>
    Task InvalidateCurrentTenantAsync(CancellationToken cancellationToken);
}
