using Endatix.Core.Abstractions.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Strips PlatformAdmin role and its permissions when local approval is missing.
/// </summary>
internal sealed class PlatformAdminLocalApprovalGate(
    AppIdentityDbContext identityDbContext,
    ILookupNormalizer keyNormalizer) : IPlatformAdminLocalApprovalGate
{
    /// <inheritdoc />
    public async Task<PlatformAdminFilteredAuthorization> ApplyAsync(
        long tenantId,
        string authProvider,
        string externalSubjectId,
        long? userId,
        string[] mappedRoles,
        string[] mappedPermissions,
        CancellationToken cancellationToken)
    {
        if (!mappedRoles.Any(SystemRole.IsPlatformAdminRoleName))
        {
            return new PlatformAdminFilteredAuthorization(mappedRoles, mappedPermissions);
        }

        var hasLocalApproval = await HasLocalPlatformAdminApprovalAsync(
            tenantId,
            authProvider,
            externalSubjectId,
            userId,
            cancellationToken);

        if (hasLocalApproval)
        {
            return new PlatformAdminFilteredAuthorization(mappedRoles, mappedPermissions);
        }

        var filteredRoles = ExcludePlatformAdminRole(mappedRoles);
        var filteredPermissions = filteredRoles.Length == 0
            ? []
            : await ResolvePermissionsForRolesAsync(filteredRoles, cancellationToken);

        return new PlatformAdminFilteredAuthorization(filteredRoles, filteredPermissions);
    }

    private async Task<bool> HasLocalPlatformAdminApprovalAsync(
        long tenantId,
        string authProvider,
        string externalSubjectId,
        long? userId,
        CancellationToken cancellationToken)
    {
        var normalizedPlatformAdminRoleName = keyNormalizer.NormalizeName(SystemRole.PlatformAdmin.Name);

        if (userId is not null)
        {
            return await identityDbContext.UserRoles
                .AsNoTracking()
                .Where(userRole => userRole.UserId == userId.Value)
                .Join(
                    identityDbContext.Roles.AsNoTracking(),
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (_, role) => role)
                .AnyAsync(role =>
                    role.IsActive &&
                    role.NormalizedName == normalizedPlatformAdminRoleName,
                    cancellationToken);
        }

        return await (
            from user in identityDbContext.Users.AsNoTracking()
            join userRole in identityDbContext.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in identityDbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where user.TenantId == tenantId
                && user.AuthProvider == authProvider
                && user.ExternalSubjectId == externalSubjectId
                && role.IsActive
                && role.NormalizedName == normalizedPlatformAdminRoleName
            select user.Id)
            .AnyAsync(cancellationToken);
    }

    private async Task<string[]> ResolvePermissionsForRolesAsync(
        string[] roleNames,
        CancellationToken cancellationToken)
    {
        var normalizedRoleNames = roleNames
            .Select(role => keyNormalizer.NormalizeName(role))
            .Distinct()
            .ToArray();

        return await identityDbContext.Roles
            .AsNoTracking()
            .Where(role => role.IsActive && normalizedRoleNames.Contains(role.NormalizedName))
            .SelectMany(role => role.RolePermissions)
            .Where(rolePermission => rolePermission.IsActive && rolePermission.Permission != null)
            .Select(rolePermission => rolePermission.Permission!.Name)
            .Where(permissionName => !string.IsNullOrEmpty(permissionName))
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    private static string[] ExcludePlatformAdminRole(string[] roles)
    {
        return roles
            .Where(role => !SystemRole.IsPlatformAdminRoleName(role))
            .ToArray();
    }
}
