using Endatix.Core.Abstractions;

namespace Endatix.Infrastructure.Multitenancy;

/// <summary>
/// Provides access to the current tenant context within a request scope
/// </summary>
public class TenantContext : ITenantContext
{
    /// <summary>
    /// Gets the current tenant ID
    /// </summary>
    public long? TenantId { get; private set; }

    /// <summary>
    /// Sets the current tenant ID
    /// </summary>
    /// <param name="tenantId">The tenant ID to set</param>
    internal void SetTenant(long? tenantId)
    {
        TenantId = tenantId;
    }
}
