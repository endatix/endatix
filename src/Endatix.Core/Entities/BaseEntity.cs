using System.ComponentModel.DataAnnotations.Schema;
using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

/// <summary>
/// Base type for persisted entities. Inherits <see cref="HasDomainEventsBase"/> so aggregates can
/// raise domain/integration events (via <c>RegisterDomainEvent</c>) that the outbox capture picks up.
/// </summary>
public abstract class BaseEntity : HasDomainEventsBase
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public virtual long Id { get; set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime? ModifiedAt { get; protected set; }
    public DateTime? DeletedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// Sets a client snowflake before the entity is tracked. EF's empty constructor leaves
    /// <see cref="Id"/> at 0 until materialization or a value generator runs on <c>Add</c>.
    /// </summary>
    protected void AssignId(long id)
    {
        Guard.Against.NegativeOrZero(id);
        Id = id;
    }

    public virtual void Delete()
    {
        if (!IsDeleted)
        {
            IsDeleted = true;
            DeletedAt = DateTime.UtcNow;
        }
    }
}
