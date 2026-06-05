namespace Endatix.Core.Entities.Identity;

/// <summary>
/// Read model for permissions available to custom roles.
/// </summary>
public sealed record PermissionListItem
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Category { get; init; }
    public bool IsSystemDefined { get; init; }
}
