namespace Endatix.Core.Abstractions;

/// <summary>
/// Represents an entity that is owned by a tenant.
/// </summary>
public interface ITenantOwned
{
    /// <summary>
    /// Gets the ID of the tenant that owns the entity.
    /// </summary>
    long TenantId { get; }
} 