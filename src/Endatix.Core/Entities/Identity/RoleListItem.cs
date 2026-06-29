namespace Endatix.Core.Entities.Identity;

/// <summary>
/// Read model for tenant role management.
/// </summary>
public sealed record RoleListItem
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsSystemDefined { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<string> Permissions { get; init; } = [];
    public int UsersCount { get; init; }
}
