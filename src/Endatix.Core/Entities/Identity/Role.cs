using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities.Identity;

/// <summary>
/// Represents the Role entity in the application domain.
/// This class is not meant for direct persistence, but to handle domain logic related to roles.
/// It encapsulates the core attributes and behaviors of a role within the application.
/// Persistence implementation is done via the <see cref="AppRole"/>
/// </summary>
public sealed class Role : BaseEntity, IAggregateRoot
{
    public Role(
        long id,
        string name,
        string? description
        )
    {
        Guard.Against.NegativeOrZero(id);
        Guard.Against.NullOrWhiteSpace(name);

        Id = id;
        Name = name;
        Description = description;
    }

    /// <summary>
    /// The role's name within the application.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// The role's description.
    /// </summary>
    public string? Description { get; private set; }
}
