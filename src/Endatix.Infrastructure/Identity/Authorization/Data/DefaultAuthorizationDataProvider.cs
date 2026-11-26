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
        var data = await ReadSessionAsync(new(userId, tenantId, actorUserId), cancellationToken);
        return data.IsSuccess ? Result.Success(data.Value with { VerifiedAssumption = null }) : data;
    }

    internal readonly record struct Session(long UserId, long TenantId, long? Actor)
    {
        internal bool MalformedActor { get; init; }
        internal bool PresentsActor => Actor.HasValue || MalformedActor;
    }

    internal async Task<Result<AuthorizationData>> ReadSessionAsync(Session session, CancellationToken cancellationToken)
    {
        try
        {
            var user = await FindUserAsync(session, cancellationToken);
            if (user is null)
            {
                return Result.Success(ToData(session, []) with { UnverifiedActor = session.PresentsActor });
            }
            var membership = await VerifyScopeAsync(user, session, cancellationToken);
            return membership.IsSuccess
                ? Result.Success(ToData(session, await RolesAsync(session, cancellationToken)) with
                { VerifiedAssumption = membership.Value, UnverifiedActor = session.PresentsActor && membership.Value is null })
                : membership.ToErrorResult<AuthorizationData>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting user permissions info for user {UserId}", session.UserId);
            return Result.Error("Failed to get user permissions info from the identity store");
        }
    }

    private Task<AppUser?> FindUserAsync(Session session, CancellationToken ct) => session.PresentsActor
        ? identityDbContext.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == session.UserId, ct)
        : userManager.FindByIdAsync(session.UserId.ToString());

    private Task<List<AppRole>> RolesAsync(Session session, CancellationToken ct)
    {
        var utcNow = dateTimeProvider.Now.UtcDateTime;
        var ids = identityDbContext.UserRoles.Where(ur => ur.UserId == session.UserId).Select(ur => ur.RoleId);
        return identityDbContext.Roles.Where(r => r.IsActive && ids.Contains(r.Id))
            .Where(IsRoleInAuthorizationScope(session.TenantId))
            .Include(r => r.RolePermissions.Where(rp => rp.IsActive && (rp.ExpiresAt == null || rp.ExpiresAt > utcNow)))
            .ThenInclude(rp => rp.Permission)
            .AsSplitQuery().AsNoTracking().ToListAsync(ct);
    }

    private static AuthorizationData ToData(Session session, List<AppRole> roles)
    {
        var names = roles.Where(IsNotBorrowingAnAdminRoleName).Select(r => r.Name!).ToArray();
        var permissions = roles.SelectMany(r => r.RolePermissions.Select(rp => rp.Permission.Name)
            .Where(name => RoleGrantCeiling.GrantsOnRead(r, name))).Distinct().ToArray();
        return AuthorizationData.ForAuthenticatedUser(session.UserId.ToString(), session.TenantId, names, permissions,
            expiresAt: EarliestPermissionExpiry(roles));
    }

    private async Task<Result<VerifiedAssumption?>> VerifyScopeAsync(AppUser user, Session session, CancellationToken ct)
    {
        if (IsUserInAuthorizationTenantScope(user, session.TenantId))
        {
            return Result<VerifiedAssumption?>.Success(null);
        }
        if (!session.MalformedActor && IsAssumeTenantSession(user.Id, session.Actor, user.TenantId, session.TenantId) && await UserHasPlatformAdminRoleAsync(user.Id, ct))
        {
            return Result.Success<VerifiedAssumption?>(new(user.Id, user.TenantId));
        }
        logger.LogWarning("Blocked authorization for user {UserId} outside tenant scope {TenantId}.", user.Id, session.TenantId);
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
