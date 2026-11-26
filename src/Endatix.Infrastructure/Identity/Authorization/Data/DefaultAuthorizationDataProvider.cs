using System.Linq.Expressions;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Services;
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
        var data = await ReadSessionAsync(userId, tenantId, cancellationToken, actorUserId);
        return data.IsSuccess ? Result.Success(data.Value with { VerifiedAssumption = null }) : data;
    }

    internal async Task<Result<AuthorizationData>> ReadSessionAsync(long userId, long tenantId, CancellationToken cancellationToken, long? actorUserId)
    {
        var utcNow = dateTimeProvider.Now.UtcDateTime;

        try
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return actorUserId.HasValue ? Result.Forbidden() : Result.Success(
                    AuthorizationData.ForAuthenticatedUser(userId.ToString(), tenantId, [], []));
            }

            var membership = await VerifyScopeAsync(user, tenantId, actorUserId, cancellationToken);
            if (!membership.IsSuccess)
            {
                return membership.ToErrorResult<AuthorizationData>();
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
                .Where(IsNotBorrowingAnAdminRoleName)
                .Select(r => r.Name!)
                .ToArray();

            var assignedPermissions = userRoles
                .SelectMany(r => r.RolePermissions
                    .Select(rp => rp.Permission.Name)
                    .Where(name => RoleGrantCeiling.GrantsOnRead(r, name)))
                .Distinct()
                .ToArray();

            var authorizationData = AuthorizationData.ForAuthenticatedUser(
                    userId: userId.ToString(), tenantId: tenantId,
                    roles: assignedRoles,
                    permissions: assignedPermissions,
                    expiresAt: EarliestPermissionExpiry(userRoles)
            );

            return Result.Success(authorizationData with { VerifiedAssumption = membership.Value });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting user permissions info for user {UserId}", userId);
            return Result.Error("Failed to get user permissions info from the identity store");
        }
    }

    private async Task<Result<VerifiedAssumption?>> VerifyScopeAsync(AppUser user, long tenantId, long? actor, CancellationToken ct)
    {
        if (actor is null && IsUserInAuthorizationTenantScope(user, tenantId))
        {
            return Result<VerifiedAssumption?>.Success(null);
        }
        if (IsAssumeTenantSession(user.Id, actor, user.TenantId, tenantId) && await UserHasPlatformAdminRoleAsync(user.Id, ct))
        {
            return Result.Success<VerifiedAssumption?>(new(user.Id, user.TenantId));
        }
        logger.LogWarning("Blocked authorization for user {UserId} outside tenant scope {TenantId}.", user.Id, tenantId);
        return Result.Forbidden("User does not belong to the requested tenant.");
    }

    /// <summary>When the first of the loaded permissions lapses, so the cache stops serving them; default when none expires.</summary>
    private static DateTime EarliestPermissionExpiry(List<AppRole> roles) =>
        roles.SelectMany(role => role.RolePermissions).Min(rolePermission => rolePermission.ExpiresAt) ?? default;

    /// <summary>
    /// Admin and PlatformAdmin grant everything by name downstream (<see cref="AuthorizationData.IsAdmin"/>), so only
    /// the built-in system rows may carry those names into the authorization data. A tenant role that borrows one
    /// keeps its permissions but not the name.
    /// </summary>
    internal static bool IsNotBorrowingAnAdminRoleName(AppRole role) =>
        !BuiltInAdminRoles.HasAdminName(role.Name) || BuiltInAdminRoles.IsBuiltInAdminRole(role);

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
            && userId > 0
            && homeTenantId > 0
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
                role => role.IsActive && role.IsSystemDefined && role.TenantId <= 0 && role.Name == platformAdminRoleName,
                cancellationToken);
    }
}
