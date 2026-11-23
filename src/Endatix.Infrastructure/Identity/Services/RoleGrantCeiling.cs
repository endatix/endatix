using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Services;

/// <summary>
/// One rule for every role change a user makes: a caller can only grant, widen, take away or hand out access they
/// hold themselves. Admin (and PlatformAdmin) holds everything; the built-in Admin and PlatformAdmin roles carry
/// everything, so only an admin may move them. A change with no signed-in user is refused; system callers use
/// <see cref="Endatix.Core.Abstractions.ISystemRoleAssignment"/> instead.
/// </summary>
/// <remarks>
/// The caller's access is the request's authorization data, the same the endpoint gate reads. It is keyed on the
/// tenant's access version, so a change committed by an earlier request is already in it.
/// </remarks>
internal sealed class RoleGrantCeiling(
    AppIdentityDbContext identityDbContext,
    IHttpContextAccessor httpContextAccessor,
    ICurrentUserAuthorizationService currentUserAuthorizationService,
    ILogger logger)
{
    internal const string CEILING_MESSAGE = "You can only grant access you hold yourself.";
    internal const string OWN_ACCESS_MESSAGE = "You cannot change your own access.";
    internal const string NO_CALLER_MESSAGE = "Changing role assignments requires a signed-in user.";
    private const string PLATFORM_PERMISSION_PREFIX = "platform.";

    private AuthorizationData? _caller;

    /// <summary>
    /// Whether the role grants the permission when access is read. A platform permission counts only on a built-in
    /// system row.
    /// </summary>
    public static bool GrantsOnRead(AppRole role, string permissionName) =>
        BuiltInAdminRoles.IsSystemRow(role) || !IsPlatformPermission(permissionName);

    /// <summary>Tenant roles never carry platform permissions, whoever asks.</summary>
    public static Result EnsureNoPlatformPermissions(IEnumerable<string> permissionNames, string identifier)
    {
        var platformPermissions = permissionNames
            .Where(IsPlatformPermission)
            .ToList();

        return platformPermissions.Count == 0
            ? Result.Success()
            : Result.Invalid(new ValidationError
            {
                Identifier = identifier,
                ErrorMessage = $"Tenant roles cannot carry platform permissions: {string.Join(", ", platformPermissions)}"
            });
    }

    /// <summary>The caller must hold every one of the permissions.</summary>
    public async Task<Result> EnsureCanGrantPermissionsAsync(IEnumerable<string> permissionNames, CancellationToken cancellationToken)
    {
        var (caller, refusal) = await LoadCallerAsync(cancellationToken);
        if (caller is null || caller.IsAdmin)
        {
            return refusal;
        }

        return HoldsAll(caller, permissionNames) ? Result.Success() : Refuse(caller);
    }

    /// <summary>
    /// The caller must hold every permission the named roles carry, each name resolved to the tenant's own and the
    /// system rows; the built-in admin roles need an admin.
    /// </summary>
    public async Task<Result> EnsureCanGrantRoleNamesAsync(
        IReadOnlyCollection<string> normalizedRoleNames,
        long tenantId,
        CancellationToken cancellationToken)
    {
        var (caller, refusal) = await LoadCallerAsync(cancellationToken);
        if (caller is null || caller.IsAdmin || normalizedRoleNames.Count == 0)
        {
            return refusal;
        }

        var roles = await identityDbContext.Roles
            .AsNoTracking()
            .Where(role =>
                normalizedRoleNames.Contains(role.NormalizedName!) &&
                (role.TenantId == tenantId || (role.IsSystemDefined && role.TenantId <= 0)))
            .ToListAsync(cancellationToken);
        return await EnsureWithinAsync(caller, roles, cancellationToken);
    }

    /// <summary>The caller must hold every permission the roles carry; the built-in admin roles need an admin.</summary>
    public async Task<Result> EnsureCanGrantRolesAsync(IReadOnlyCollection<AppRole> roles, CancellationToken cancellationToken)
    {
        var (caller, refusal) = await LoadCallerAsync(cancellationToken);
        if (caller is null || caller.IsAdmin || roles.Count == 0)
        {
            return refusal;
        }

        return await EnsureWithinAsync(caller, roles, cancellationToken);
    }

    private async Task<Result> EnsureWithinAsync(
        AuthorizationData caller,
        IReadOnlyCollection<AppRole> roles,
        CancellationToken cancellationToken)
    {
        if (roles.Any(BuiltInAdminRoles.IsBuiltInAdminRole))
        {
            return Refuse(caller);
        }

        var roleIds = roles.Select(role => role.Id).ToList();
        var permissionNames = await identityDbContext.RolePermissions
            .AsNoTracking()
            .Where(rolePermission => rolePermission.IsActive && roleIds.Contains(rolePermission.RoleId))
            .Select(rolePermission => rolePermission.Permission.Name)
            .Distinct()
            .ToListAsync(cancellationToken);

        return HoldsAll(caller, permissionNames) ? Result.Success() : Refuse(caller);
    }

    /// <summary>A non-admin caller may not change their own role set.</summary>
    public async Task<Result> EnsureNotOwnRolesAsync(long userId, CancellationToken cancellationToken)
    {
        var (caller, refusal) = await LoadCallerAsync(cancellationToken);
        if (caller is null || caller.IsAdmin)
        {
            return refusal;
        }

        return caller.UserId == userId.ToString() ? RefuseOwnAccess(caller) : Result.Success();
    }

    /// <summary>A non-admin caller may not change the permissions of a role they hold.</summary>
    public async Task<Result> EnsureNotOwnRoleAsync(long roleId, CancellationToken cancellationToken)
    {
        var (caller, refusal) = await LoadCallerAsync(cancellationToken);
        if (caller is null || caller.IsAdmin)
        {
            return refusal;
        }

        var callerId = long.Parse(caller.UserId);
        var holdsRole = await identityDbContext.UserRoles
            .AnyAsync(userRole => userRole.UserId == callerId && userRole.RoleId == roleId, cancellationToken);
        return holdsRole ? RefuseOwnAccess(caller) : Result.Success();
    }

    /// <summary>
    /// The signed-in caller's access, or the refusal when there is none: no signed-in user, or a signed-in user whose
    /// access cannot be read. The refusal is <see cref="Result.Success()"/> when a caller was found.
    /// </summary>
    private async Task<(AuthorizationData? Caller, Result Refusal)> LoadCallerAsync(CancellationToken cancellationToken)
    {
        if (_caller is not null)
        {
            return (_caller, Result.Success());
        }

        if (httpContextAccessor.HttpContext?.User.GetUserId() is null)
        {
            logger.LogWarning("Blocked a role change with no signed-in user.");
            return (null, Result.Forbidden(NO_CALLER_MESSAGE));
        }

        var data = await currentUserAuthorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!data.IsSuccess || !long.TryParse(data.Value.UserId, out _))
        {
            logger.LogWarning("Blocked a role change: the caller's access could not be read.");
            return (null, Result.Forbidden(CEILING_MESSAGE));
        }

        _caller = data.Value;
        return (_caller, Result.Success());
    }

    private static bool IsPlatformPermission(string permissionName) =>
        permissionName.StartsWith(PLATFORM_PERMISSION_PREFIX, StringComparison.OrdinalIgnoreCase);

    private static bool HoldsAll(AuthorizationData caller, IEnumerable<string> permissionNames) =>
        permissionNames.All(caller.Permissions.Contains);

    private Result Refuse(AuthorizationData caller)
    {
        logger.LogWarning("Blocked a role change by user {UserId}: it exceeds the user's own access.", caller.UserId);
        return Result.Forbidden(CEILING_MESSAGE);
    }

    private Result RefuseOwnAccess(AuthorizationData caller)
    {
        logger.LogWarning("Blocked a role change by user {UserId}: it targets the user's own access.", caller.UserId);
        return Result.Forbidden(OWN_ACCESS_MESSAGE);
    }
}
