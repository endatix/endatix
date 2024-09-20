using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities.Identity;

/// <summary>
/// Represents the User entity in the application domain.
/// This class is not meant for direct persistence, but to handle domain logic related to users.
/// It encapsulates the core attributes and behaviors of a user within the application.
/// Persistence implementation is done via the <see cref="AppUser"/>
/// </summary>
public sealed class User : BaseEntity, IAggregateRoot
{
    public User(
        long id,
        string externalId,
        string userName,
        string email,
        bool isVerified
        )
    {
        Guard.Against.Null(id);

        Id = id;
        ExternalId = externalId;
        UserName = userName;
        Email = email;
        IsVerified = isVerified;
    }

    /// <summary>
    /// External identifier for the user, typically used for integration with external systems.
    /// </summary>
    public string ExternalId { get; private set; }

    /// <summary>
    /// The user's chosen username for the application.
    /// </summary>
    public string? UserName { get; private set; }

    /// <summary>
    /// The user's email address.
    /// </summary>
    public string? Email { get; private set; }

    /// <summary>
    /// Indicates whether the user's account is verified, typically through email confirmation.
    /// </summary>
    public bool IsVerified { get; private set; }
}