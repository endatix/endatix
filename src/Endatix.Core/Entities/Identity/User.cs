using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities.Identity;

/// <summary>
/// Represents the User entity in the application domain.
/// This class is not meant for direct persistence, but to handle domain logic related to users.
/// It encapsulates the core attributes and behaviors of a user within the application.
/// Persistence implementation is done via the <see cref="AppUser"/>
/// </summary>
public sealed class User : TenantEntity, IAggregateRoot
{
    public User(
        long id,
        string userName,
        string email,
        bool isVerified
        ) : base()
    {
        Initialize(id, userName, email, isVerified);
    }

    public User(
        long id,
        long tenantId,
        string userName,
        string email,
        bool isVerified
        ) : base(tenantId)
    {
        Initialize(id, userName, email, isVerified);
    }

    private void Initialize(
        long id,
        string userName,
        string email,
        bool isVerified)
    {
        Guard.Against.NegativeOrZero(id);
        Guard.Against.NullOrWhiteSpace(userName);
        Guard.Against.NullOrWhiteSpace(email);

        Id = id;
        UserName = userName;
        Email = email;
        IsVerified = isVerified;
    }

    /// <summary>
    /// The user's chosen username for the application.
    /// </summary>
    public string UserName { get; private set; } = null!;

    /// <summary>
    /// The user's email address.
    /// </summary>
    public string Email { get; private set; } = null!;

    /// <summary>
    /// Indicates whether the user's account is verified, typically through email confirmation.
    /// </summary>
    public bool IsVerified { get; private set; }
}
