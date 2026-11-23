using Endatix.Infrastructure.Identity.Authorization.Data;
using Endatix.Infrastructure.Identity.Services;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Reads roles by name the way every access read does: the tenant's own roles and the built-in system rows only,
/// Admin and PlatformAdmin names only from a built-in row, and platform permissions only from a built-in row.
/// </summary>
internal static class ScopedRoleRead
{
    /// <summary>Active roles with one of the normalized names, from the tenant or a built-in system row.</summary>
    public static IQueryable<AppRole> InTenantScope(
        this IQueryable<AppRole> roles,
        IReadOnlyCollection<string> normalizedRoleNames,
        long tenantId) =>
        roles
            .Where(role => role.IsActive && normalizedRoleNames.Contains(role.NormalizedName!))
            .Where(DefaultAuthorizationDataProvider.IsRoleInAuthorizationScope(tenantId));

    /// <summary>The role names access may carry: an Admin or PlatformAdmin name only from a built-in row.</summary>
    public static string[] GrantedRoleNames(IEnumerable<AppRole> roles) =>
        roles
            .Where(DefaultAuthorizationDataProvider.IsNotBorrowingAnAdminRoleName)
            .Where(role => role.Name is not null)
            .Select(role => role.Name!)
            .Distinct()
            .ToArray();

    /// <summary>The active permissions the roles grant when access is read.</summary>
    public static string[] GrantedPermissions(IEnumerable<AppRole> rolesWithPermissions) =>
        rolesWithPermissions
            .SelectMany(role => role.RolePermissions
                .Where(rolePermission =>
                    rolePermission.IsActive &&
                    rolePermission.Permission is not null &&
                    !string.IsNullOrEmpty(rolePermission.Permission.Name))
                .Select(rolePermission => rolePermission.Permission.Name)
                .Where(name => RoleGrantCeiling.GrantsOnRead(role, name)))
            .Distinct()
            .ToArray();
}
