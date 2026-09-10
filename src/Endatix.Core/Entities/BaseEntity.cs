using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

/// <summary>
/// Base type for persisted entities. Inherits <see cref="HasDomainEventsBase"/> so aggregates can
/// raise domain/integration events (via <c>RegisterDomainEvent</c>) that the outbox capture picks up.
/// </summary>
public abstract class BaseEntity : HasDomainEventsBase
{
    /// <summary>
    /// Client-assigned snowflake, never a database <c>IDENTITY</c>/serial. Value generation is
    /// configured once per context by
    /// <c>DbContextModelBuilderExtensions.ApplySnowflakeIdValueGenerators</c> (EF <c>OnAdd</c> client
    /// generator); the explicit-Id <c>Create(long id, …)</c> factories set it for tests, imports,
    /// seeding and data migrations.
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
