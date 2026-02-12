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
    public string Email { get; init; } = null!;

    /// <summary>
    /// Indicates whether the user's email is verified.
    /// </summary>
    public bool IsVerified { get; init; }

    /// <summary>
    /// The role names assigned to the user.
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];
}
