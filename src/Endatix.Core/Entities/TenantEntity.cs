using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;

namespace Endatix.Core.Entities;

public abstract class TenantEntity : BaseEntity, ITenantOwned
{
    protected TenantEntity(long tenantId)
    {
        Guard.Against.NegativeOrZero(tenantId, nameof(tenantId));
        TenantId = tenantId;
    }

    protected TenantEntity() { } // For EF Core

    public long TenantId { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
}
