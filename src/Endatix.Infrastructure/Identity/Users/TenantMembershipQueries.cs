using Endatix.Core.Abstractions.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Endatix.Infrastructure.Identity.Users;

/// <summary>
/// Membership is last-used <see cref="AppUser.TenantId"/> plus tenant-scoped role assignments
/// (<c>AppRole.TenantId == tenant</c>). Shared <c>TenantId = 0</c> system roles are permission
/// templates, not membership rows.
/// </summary>
internal static class TenantMembershipQueries
{
    internal static IQueryable<AppUser> MembersOf(
        this IQueryable<AppUser> users,
        IQueryable<IdentityUserRole<long>> userRoles,
        IQueryable<AppRole> roles,
        long tenantId)
    {
        return users.Where(user =>
            user.TenantId == tenantId
            || userRoles.Any(userRole =>
                userRole.UserId == user.Id
                && roles.Any(role => role.Id == userRole.RoleId && role.TenantId == tenantId)));
    }

    internal static IQueryable<IdentityUserRole<long>> TenantScopedRoleAssignments(
        this IQueryable<IdentityUserRole<long>> userRoles,
        IQueryable<AppRole> roles,
        long userId,
        long tenantId)
    {
        return userRoles
            .Where(userRole => userRole.UserId == userId)
            .Join(
                roles.Where(role => role.TenantId == tenantId),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, _) => userRole);
    }

    internal static IQueryable<IdentityUserRole<long>> SharedNonPlatformSystemAssignments(
        this IQueryable<IdentityUserRole<long>> userRoles,
        IQueryable<AppRole> roles,
        long userId)
    {
        return userRoles
            .Where(userRole => userRole.UserId == userId)
            .Join(
                roles.Where(role =>
                    role.IsSystemDefined
                    && role.TenantId <= 0
                    && role.Name != SystemRole.PlatformAdmin.Name),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, _) => userRole);
    }

    internal static IQueryable<long> MembershipTenantIds(
        IQueryable<AppUser> users,
        IQueryable<IdentityUserRole<long>> userRoles,
        IQueryable<AppRole> roles,
        long userId)
    {
        var homeTenantId = users
            .Where(user => user.Id == userId && user.TenantId > 0)
            .Select(user => user.TenantId);

        var roleTenantIds = userRoles
            .Where(userRole => userRole.UserId == userId)
            .Join(
                roles.Where(role => role.TenantId > 0),
                userRole => userRole.RoleId,
                role => role.Id,
                (_, role) => role.TenantId);

        return homeTenantId.Concat(roleTenantIds).Distinct();
    }
}
