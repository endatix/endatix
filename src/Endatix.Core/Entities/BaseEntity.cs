using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

/// <summary>
/// Base type for persisted entities. Inherits <see cref="HasDomainEventsBase"/> so aggregates can
/// raise domain/integration events (via <c>RegisterDomainEvent</c>) that the outbox capture picks up.
/// </summary>
public abstract class BaseEntity : HasDomainEventsBase
{
    /// <summary>
    /// Client snowflake, never IDENTITY/serial. EF OnAdd generator
    /// (<c>ApplySnowflakeIdValueGenerators</c>) stamps it; <c>Create(long id, …)</c> sets it for tests/seed/import.
    /// </summary>
    public virtual long Id { get; set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime? ModifiedAt { get; protected set; }
    public DateTime? DeletedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    public virtual void Delete()
    {
        if (!IsDeleted)
        {
            IsDeleted = true;
            DeletedAt = DateTime.UtcNow;
        }
    }
}
