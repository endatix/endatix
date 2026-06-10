namespace Endatix.Core.Entities.Identity;

/// <summary>
/// User with assigned role names, used when listing users for the current tenant.
/// </summary>
public sealed record UserWithRoles
{
    /// <summary>
    /// The user's unique identifier.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    public string UserName { get; init; } = null!;

    /// <summary>
    /// The user's email address.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Indicates whether the user's email is verified.
    /// </summary>
    public bool IsVerified { get; init; }

    /// <summary>
    /// Authentication provider for this user.
    /// </summary>
    public string AuthProvider { get; init; } = string.Empty;

    /// <summary>
    /// Indicates whether the user is managed by an external identity provider.
    /// </summary>
    public bool IsExternal { get; init; }

    /// <summary>
    /// Indicates whether the user is locally locked out.
    /// </summary>
    public bool IsLockedOut { get; init; }

    /// <summary>
    /// External or friendly display name for the user.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// The last time the user authenticated successfully.
    /// </summary>
    public DateTimeOffset? LastLoginAt { get; init; }

    /// <summary>
    /// The role names assigned to the user.
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];
}
