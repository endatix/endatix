using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Microsoft.AspNetCore.Identity;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// Enhanced ASP.NET IdentityRole with permission management capabilities.
/// This serves as both the persistence entity and contains domain logic for roles.
/// </summary>
public class AppRole : IdentityRole<long>, ITenantOwned
{
    private readonly List<RolePermission> _rolePermissions = [];

    /// <summary>
    /// Description for the role. 
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The ID of the tenant this role belongs to.
    /// </summary>
    public long TenantId { get; set; }

    /// <summary>
    /// Whether this role is system-defined and cannot be deleted
    /// </summary>
    public bool IsSystemDefined { get; set; }

    /// <summary>
    /// Whether this role is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Permissions assigned to this role
    /// </summary>
    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions.AsReadOnly();

    /// <summary>
    /// Gets all effective (active and non-expired) permissions for this role
    /// </summary>
    public IEnumerable<RolePermission> EffectivePermissions => 
        _rolePermissions.Where(rp => rp.IsEffective);

    /// <summary>
    /// Creates a system-defined role that cannot be deleted.
    /// </summary>
    public static AppRole CreateSystemRole(string name, string? description = null, long tenantId = 0)
    {
        return new AppRole
        {
            Name = name,
            Description = description,
            TenantId = tenantId,
            IsSystemDefined = true,
            IsActive = true
        };
    }

    public void UpdateDescription(string? description)
    {
        Description = description;
    }

    public void AddPermission(Permission permission, DateTime? expiresAt = null)
    {
        Guard.Against.Null(permission);

        if (HasPermission(permission.Id))
        {
            throw new InvalidOperationException($"Role already has permission '{permission.Name}'");
        }

        var rolePermission = new RolePermission(Id, permission.Id, expiresAt);
        _rolePermissions.Add(rolePermission);
    }

    public void RemovePermission(long permissionId)
    {
        var rolePermission = _rolePermissions.FirstOrDefault(rp => rp.PermissionId == permissionId);
        rolePermission?.Revoke();
    }

    public bool HasPermission(long permissionId)
    {
        return _rolePermissions.Any(rp => rp.PermissionId == permissionId && rp.IsEffective);
    }

    public bool HasPermission(string permissionName)
    {
        return _rolePermissions.Any(rp => 
            rp.IsEffective && 
            rp.Permission.Name == permissionName);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        if (IsSystemDefined)
        {
            throw new InvalidOperationException("Cannot deactivate system-defined roles.");
        }
        IsActive = false;
    }

    public void Delete()
    {
        if (IsSystemDefined)
        {
            throw new InvalidOperationException("Cannot delete system-defined roles.");
        }
        // Mark as deleted - actual deletion handled by Identity framework
        IsActive = false;
    }
}