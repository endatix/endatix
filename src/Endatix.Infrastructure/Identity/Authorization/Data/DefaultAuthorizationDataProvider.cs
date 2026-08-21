using System.Linq.Expressions;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Authorization.Data;

/// <summary>
/// Default implementation of <see cref="IAuthorizationDataProvider"/> that uses the user manager and identity database context.
/// </summary>
/// <param name="userManager">The user manager.</param>
/// <param name="identityDbContext">The identity database context.</param>
/// <param name="dateTimeProvider">The date time provider.</param>
/// <param name="logger">The logger.</param>
internal sealed class DefaultAuthorizationDataProvider(
    UserManager<AppUser> userManager,
    AppIdentityDbContext identityDbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<DefaultAuthorizationDataProvider> logger) : IAuthorizationDataProvider
{
    /// <inheritdoc />
    public async Task<Result<AuthorizationData>> GetAuthorizationDataAsync(
        long userId,
        long tenantId,
        CancellationToken cancellationToken,
        long? actorUserId = null)
    {
        var utcNow = dateTimeProvider.Now.UtcDateTime;

        try
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                var anonymousData = AuthorizationData.ForAuthenticatedUser(
                    userId: userId.ToString(),
                    tenantId: tenantId,
                    roles: [],
                    permissions: []
                    );

                return Result.Success(anonymousData);
            }

            if (!IsUserInAuthorizationTenantScope(user, tenantId))
            {
                var assumed = actorUserId is long actorId
                    && actorId == user.Id
                    && tenantId > 0
                    && await UserHasPlatformAdminRoleAsync(user.Id, cancellationToken);
                if (!assumed)
                {
                    logger.LogWarning(
                        "Blocked authorization data request for user {UserId} with mismatched tenant scope {TenantId}. User belongs to tenant {UserTenantId}.",
                        userId,
                        tenantId,
                        user.TenantId);
                    return Result.Forbidden("User does not belong to the requested tenant.");
                }
            }

            var userRoleIds = identityDbContext.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.RoleId);

            var userRoles = await identityDbContext.Roles
                .Where(r =>
                    r.IsActive &&
                    userRoleIds.Contains(r.Id))
                .Where(IsRoleInAuthorizationScope(tenantId))
                .Include(r => r.RolePermissions.Where(rp => rp.IsActive && (rp.ExpiresAt == null || rp.ExpiresAt > utcNow)))
                .ThenInclude(rp => rp.Permission)
                .AsSplitQuery()
                .AsNoTracking()
                .ToListAsync(cancellationToken);


            var assignedRoles = userRoles
                .Select(r => r.Name!)
                .ToArray();

            var assignedPermissions = userRoles
                .SelectMany(r => r.RolePermissions.Select(rp => rp.Permission.Name))
                .Distinct()
                .ToArray();

            var authorizationData = AuthorizationData.ForAuthenticatedUser(
                    userId: userId.ToString(),
                    tenantId: tenantId,
                    roles: assignedRoles,
                    permissions: assignedPermissions
            );

            return Result.Success(authorizationData);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting user permissions info for user {UserId}", userId);
            return Result.Error("Failed to get user permissions info from the identity store");
        }
    }

    internal static Expression<Func<AppRole, bool>> IsRoleInAuthorizationScope(long tenantId)
    {
        return role => role.TenantId == tenantId || (role.IsSystemDefined && role.TenantId <= 0);
    }

    internal static bool IsUserInAuthorizationTenantScope(AppUser user, long tenantId)
    {
        return user.TenantId == tenantId;
    }

    internal static bool IsAssumeTenantSession(long userId, long? actorUserId, long homeTenantId, long requestedTenantId)
    {
        return actorUserId is long actorId
            && actorId == userId
            && requestedTenantId > 0
            && homeTenantId != requestedTenantId;
    }

    private async Task<bool> UserHasPlatformAdminRoleAsync(long userId, CancellationToken cancellationToken)
    {
        var platformAdminRoleName = SystemRole.PlatformAdmin.Name;
        return await identityDbContext.UserRoles
            .Where(userRole => userRole.UserId == userId)
            .Join(
                identityDbContext.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (_, role) => role)
            .AnyAsync(
                role => role.IsActive && role.Name == platformAdminRoleName,
                cancellationToken);
    }
}

