using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities.Identity;

public sealed class Permission : BaseEntity, IAggregateRoot
{
    private readonly List<RolePermission> _rolePermissions = [];

    private Permission() { } // For EF Core

    public Permission(string name, string? description = null, string? category = null)
    {
        Guard.Against.NullOrEmpty(name, nameof(name));

        Name = name;
        Description = description;
        Category = category;
        IsSystemDefined = false;
        IsActive = true;
    }

    /// <summary>
    /// Create a system defined permission
    /// </summary>
    /// <param name="name">The name of the permission</param>
    /// <param name="description">The description of the permission</param>
    /// <param name="category">The category of the permission</param>
    /// <returns>The created permission</returns>
    public static Permission CreateSystemPermission(string name, string description, string category)
    {
        Guard.Against.NullOrEmpty(name, nameof(name));
        Guard.Against.NullOrEmpty(description, nameof(description));
        Guard.Against.NullOrEmpty(category, nameof(category));

        var permission = new Permission(name, description, category)
        {
            IsSystemDefined = true
        };

        return permission;
    }

    /// <summary>
    /// Unique permission nme (e.g. "forms.create", "submissions.view")
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Human friendly description of the permission (e.g. "Create new forms")
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Category for grouping permissions (e.g. "Forms", "Submissions", "User Management")
    /// </summary>
    public string? Category { get; private set; }

    /// <summary>
    /// Whether the permission is system defined and cannot be deleted
    /// </summary>
    public bool IsSystemDefined { get; private set; }

    /// <summary>
    /// Whether the permission is active and can be used
    /// </summary>
    public bool IsActive { get; private set; }


    /// <summary>
    /// Roles that have this permission
    /// </summary>
    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions.AsReadOnly();

    public void UpdateDescription(string? description)
    {
        Description = description;
    }

    public void UpdateCategory(string? category)
    {
        Category = category;
    }

    /// <summary>
    /// Activate the permission is not active
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the permission is system defined and cannot be activated.</exception>
    public void Activate()
    {
        if (IsActive is true)
        {
            return;
        }

        if (IsSystemDefined)
        {
            throw new InvalidOperationException("System defined permissions cannot be activated.");
        }

        IsActive = true;
    }

    /// <summary>
    /// Deactivate the permission if it is active
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the permission is system defined and cannot be deactivated.</exception>
    public void Deactivate()
    {
        if (IsActive is false)
        {
            return;
        }

        if (IsSystemDefined)
        {
            throw new InvalidOperationException("System defined permissions cannot be deactivated.");
        }

        IsActive = false;
    }

    /// <summary>
    /// Delete the permission if it is not system defined
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the permission is system defined and cannot be deleted.</exception>
    public override void Delete()
    {
        if (IsSystemDefined)
        {
            throw new InvalidOperationException("System defined permissions cannot be deleted.");
        }

        base.Delete();
    }
}
