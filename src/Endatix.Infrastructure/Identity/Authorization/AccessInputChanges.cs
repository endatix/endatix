using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Works out, from one save's tracked entries, which <see cref="AccessVersion"/> rows the save moves: the tenant each
/// changed access input belongs to, and the system row for system roles and platform admins.
/// </summary>
/// <remarks>
/// Collected before the save runs, because a deleted role or user takes its cascaded rows with it. A role, user role
/// or role permission whose tenant cannot be found moves the system row, which is never wrong, only broader.
/// </remarks>
internal sealed class AccessInputChanges(IReadOnlySet<long> usersAddedInTransaction)
{
    private readonly HashSet<long> _rows = [];
    private readonly HashSet<long> _rolePermissionRoleIds = [];
    private readonly List<(long UserId, long RoleId)> _userRoles = [];
    private readonly HashSet<long> _userRoleRoleIds = [];
    private readonly HashSet<long> _userIds = [];
    private readonly Dictionary<long, long> _trackedRoleTenants = [];
    private readonly Dictionary<long, long> _trackedUserTenants = [];

    /// <summary>Records one tracked entry of the save.</summary>
    public void Add(EntityEntry entry)
    {
        if (!entry.Metadata.IsAccessInput())
        {
            return;
        }

        switch (entry.Entity)
        {
            case AppRole role:
                _trackedRoleTenants[role.Id] = role.TenantId;
                AddRole(entry, role);
                break;
            case AppUser user:
                _trackedUserTenants[user.Id] = user.TenantId;
                AddUser(entry, user);
                break;
            case IdentityUserRole<long> userRole when IsChanged(entry):
                _userRoles.Add((userRole.UserId, userRole.RoleId));
                break;
            case RolePermission rolePermission when IsChanged(entry):
                _rolePermissionRoleIds.Add(rolePermission.RoleId);
                break;
            case IdentityUserRole<long> or RolePermission:
                break;
            default:
                // Permissions are global; an access input without a rule of its own is treated the same way.
                if (IsChanged(entry))
                {
                    _rows.Add(AccessVersion.SYSTEM_TENANT_ID);
                }

                break;
        }
    }

    /// <summary>The rows the save moves, looking up the tenants of the roles and users it touched.</summary>
    public IReadOnlyCollection<long> Resolve(AppIdentityDbContext db) =>
        PrepareLookups() ? Apply(RoleFacts(db).ToList(), UserFacts(db).ToList()) : _rows;

    /// <inheritdoc cref="Resolve"/>
    public async Task<IReadOnlyCollection<long>> ResolveAsync(AppIdentityDbContext db, CancellationToken cancellationToken) =>
        PrepareLookups()
            ? Apply(await RoleFacts(db).ToListAsync(cancellationToken), await UserFacts(db).ToListAsync(cancellationToken))
            : _rows;

    /// <summary>
    /// Judges user roles once the scan is done, since a new user and their roles come in any order within one save:
    /// a role granted to a user created in the same transaction (self-registration) cannot be in any cache yet.
    /// </summary>
    private bool PrepareLookups()
    {
        foreach (var (userId, roleId) in _userRoles.Where(userRole => !usersAddedInTransaction.Contains(userRole.UserId)))
        {
            _userIds.Add(userId);
            _userRoleRoleIds.Add(roleId);
        }

        return _rolePermissionRoleIds.Count > 0 || _userRoleRoleIds.Count > 0 || _userIds.Count > 0;
    }

    private static bool IsChanged(EntityEntry entry) =>
        entry.State is EntityState.Added or EntityState.Deleted or EntityState.Modified;

    private void AddRole(EntityEntry entry, AppRole role)
    {
        if (!IsChanged(entry))
        {
            return;
        }

        _rows.Add(AccessVersion.RowFor(role.TenantId));
        if (entry.State == EntityState.Modified)
        {
            _rows.Add(AccessVersion.RowFor(entry.Property(nameof(AppRole.TenantId)).OriginalValue as long? ?? 0));
        }
    }

    /// <summary>A new user holds no token yet; a deleted user or one moved to another tenant does (both tenants move).</summary>
    private void AddUser(EntityEntry entry, AppUser user)
    {
        if (entry.State == EntityState.Deleted)
        {
            _userIds.Add(user.Id);
            return;
        }

        var tenantId = entry.Property(nameof(AppUser.TenantId));
        if (entry.State == EntityState.Modified && tenantId.IsModified && !Equals(tenantId.OriginalValue, tenantId.CurrentValue))
        {
            // Only a changed value counts: UserManager.UpdateAsync marks every column modified.
            _userIds.Add(user.Id);
            _rows.Add(AccessVersion.RowFor((long)tenantId.OriginalValue!));
        }
    }

    private IQueryable<RoleFact> RoleFacts(AppIdentityDbContext db)
    {
        var roleIds = _rolePermissionRoleIds.Concat(_userRoleRoleIds).ToList();
        return db.Roles.AsNoTracking()
            .Where(role => roleIds.Contains(role.Id))
            .Select(role => new RoleFact(role.Id, role.TenantId, role.Name));
    }

    private IQueryable<UserFact> UserFacts(AppIdentityDbContext db)
    {
        var userIds = _userIds.ToList();
        var platformAdmin = SystemRole.PlatformAdmin.Name;
        return db.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new UserFact(
                user.Id,
                user.TenantId,
                db.UserRoles.Any(userRole => userRole.UserId == user.Id
                    && db.Roles.Any(role => role.Id == userRole.RoleId && role.IsActive && role.Name == platformAdmin))));
    }

    private HashSet<long> Apply(List<RoleFact> roles, List<UserFact> users)
    {
        HashSet<long> rows = [.. _rows];
        foreach (var roleId in _rolePermissionRoleIds)
        {
            rows.Add(AccessVersion.RowFor(RoleTenant(roleId, roles)));
        }

        if (_userRoleRoleIds.Any(roleId => roles.Find(role => role.Id == roleId)?.Name == SystemRole.PlatformAdmin.Name))
        {
            rows.Add(AccessVersion.SYSTEM_TENANT_ID);
        }

        foreach (var userId in _userIds)
        {
            AddUserRows(rows, userId, users.Find(user => user.Id == userId));
        }

        return rows;
    }

    /// <summary>
    /// A user's own sessions key on their tenant; a platform admin's assumed-tenant sessions key on other tenants and
    /// the system version, so a platform admin's change moves the system row too.
    /// </summary>
    private void AddUserRows(HashSet<long> rows, long userId, UserFact? stored)
    {
        if (stored is not null)
        {
            rows.Add(AccessVersion.RowFor(stored.TenantId));
        }

        if (_trackedUserTenants.TryGetValue(userId, out var tracked))
        {
            rows.Add(AccessVersion.RowFor(tracked));
        }

        if ((stored is null && !_trackedUserTenants.ContainsKey(userId)) || stored?.IsPlatformAdmin == true)
        {
            rows.Add(AccessVersion.SYSTEM_TENANT_ID);
        }
    }

    private long RoleTenant(long roleId, List<RoleFact> roles)
    {
        if (roles.Find(role => role.Id == roleId) is { } stored)
        {
            return stored.TenantId;
        }

        return _trackedRoleTenants.TryGetValue(roleId, out var tracked) ? tracked : AccessVersion.SYSTEM_TENANT_ID;
    }

    private sealed record RoleFact(long Id, long TenantId, string? Name);

    private sealed record UserFact(long Id, long TenantId, bool IsPlatformAdmin);
}
