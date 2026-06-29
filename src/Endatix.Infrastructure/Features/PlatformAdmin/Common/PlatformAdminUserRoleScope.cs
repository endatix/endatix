using Endatix.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Endatix.Infrastructure.Features.PlatformAdmin.Common;

/// <summary>
/// Applies local PlatformAdmin role membership filters to user queries.
/// </summary>
internal static class PlatformAdminUserRoleScope
{
    public static IQueryable<AppUser> Apply(
        IQueryable<AppUser> usersQuery,
        IQueryable<IdentityUserRole<long>> userRoles,
        long? platformAdminRoleId,
        PlatformAdminUserScopeFilter scopeFilter)
    {
        if (scopeFilter == PlatformAdminUserScopeFilter.IgnoreLocalPlatformAdminRole ||
            platformAdminRoleId is null)
        {
            return usersQuery;
        }

        var platformAdminUserIds = userRoles
            .Where(userRole => userRole.RoleId == platformAdminRoleId.Value)
            .Select(userRole => userRole.UserId);

        return scopeFilter switch
        {
            PlatformAdminUserScopeFilter.MustHaveLocalPlatformAdminRole =>
                usersQuery.Where(user => platformAdminUserIds.Contains(user.Id)),
            PlatformAdminUserScopeFilter.MustNotHaveLocalPlatformAdminRole =>
                usersQuery.Where(user => !platformAdminUserIds.Contains(user.Id)),
            _ => usersQuery,
        };
    }
}
