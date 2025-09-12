using System;
using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities.Identity;

public sealed class RolePermission : BaseEntity
{
    private RolePermission() { } // For EF Core

    public RolePermission(long roleId, long permissionId, DateTime? expiresAt = null)
    {
        Guard.Against.NegativeOrZero(roleId, nameof(roleId));
        Guard.Against.NegativeOrZero(permissionId, nameof(permissionId));

        RoleId = roleId;
        PermissionId = permissionId;
        ExpiresAt = expiresAt;
        IsActive = true;
        GrantedAt = DateTime.UtcNow;
    }

    public long RoleId { get; private set; }
    public long PermissionId { get; private set; }

    /// <summary>
    /// The date and time the permission was granted to the role
    /// </summary>
    public DateTime GrantedAt { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// The date and time the permission will expire. Optional for temporary granted permissions.
    /// </summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>
    /// Whether the permission is active
    /// </summary>
    public bool IsActive { get; private set; } = true;

    // Navigation properties
    public Permission Permission { get; private set; } = null!;

    public bool IsExpired => ExpiresAt is not null && ExpiresAt < DateTime.UtcNow;
    public bool IsEffective => IsActive && !IsExpired;

    /// <summary>
    /// Revoke the permission from the role
    /// </summary>
    public void Revoke()
    {
        IsActive = false;
    }

    /// <summary>
    /// Grant the permission to the role
    /// </summary>
    public void Grant()
    {
        IsActive = true;
    }

    /// <summary>
    /// Set the expiration date for the permission
    /// </summary>
    /// <param name="expiresAt">The expiration date</param>
    /// <exception cref="InvalidOperationException">Thrown when the expiration date is in the past</exception>
    public void SetExpiration(DateTime expiresAt)
    {
        if (expiresAt < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Expiration date cannot be in the past");
        }

        ExpiresAt = expiresAt;
    }
}
