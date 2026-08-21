using System.Security.Claims;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Core.UseCases.Identity.ListUsers;
using Microsoft.AspNetCore.Identity;
using Endatix.Infrastructure.Data.Querying;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Users;

/// <summary>
/// Implements the user service by leveraging the integration with ASP.NET Core Identity including the registered <see cref="AppUser" /> persisted object />
/// </summary>
public sealed class AppUserService(
    UserManager<AppUser> userManager,
    ITenantContext tenantContext,
    AppIdentityDbContext identityDbContext,
    IEmailVerificationService emailVerificationService,
    IUserContext userContext,
    IRelationalSubstringLikeFilter substringLikeFilter,
    IAuthorizationCache authorizationCache,
    ILogger<AppUserService> logger) : IUserService
{
    private const string SelfRemovalForbiddenMessage = "You cannot remove your own tenant access.";
    private const string LastTenantAdminRemovalForbiddenMessage = "Cannot remove the last active tenant admin.";
    private const string SelfLockoutForbiddenMessage = "You cannot lock out your own account.";

    /// <inheritdoc />
    public async Task<Result<Paged<UserWithRoles>>> ListUsersAsync(
        SearchablePageRequest paging,
        UserListCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var skip = paging.Paging.Skip;
        var take = paging.Paging.PageSize;
        var tenantId = tenantContext.TenantId;

        var filteredUsers = identityDbContext.Users
            .AsNoTracking()
            .MembersOf(identityDbContext.UserRoles, identityDbContext.Roles, tenantId)
            .Where(user =>
                user.UserName != null &&
                user.UserName != string.Empty);

        filteredUsers = ApplyStatusFilter(filteredUsers, criteria.Status);
        filteredUsers = ApplyRoleFilter(filteredUsers, criteria.Role);
        filteredUsers = ApplySearchFilter(filteredUsers, paging.Search);
        filteredUsers = filteredUsers.WhereUtcRange(user => user.LastLoginAt, criteria.LastLogin);

        var totalRecords = await filteredUsers.CountAsync(cancellationToken);
        var effectiveSkip = NormalizeSkip(skip, take, totalRecords);

        var pageUsers = await ApplyUserListOrdering(filteredUsers, criteria.Sort)
            .Skip(effectiveSkip)
            .Take(take)
            .Select(user => new
            {
                user.Id,
                user.TenantId,
                UserName = user.UserName!,
                user.Email,
                user.EmailConfirmed,
                user.AuthProvider,
                user.LockoutEnd,
                user.DisplayName,
                user.LastLoginAt,
                user.ExternalRolesJson
            })
            .ToListAsync(cancellationToken);

        var userIds = pageUsers.Select(user => user.Id).ToList();
        var rolesByUserId = new Dictionary<long, List<string>>();

        if (userIds.Count > 0)
        {
            var roleRows = await identityDbContext.UserRoles
                .AsNoTracking()
                .Where(userRole => userIds.Contains(userRole.UserId))
                .Join(
                    identityDbContext.Roles.AsNoTracking(),
                    userRole => userRole.RoleId,
                    appRole => appRole.Id,
                    (userRole, appRole) => new
                    {
                        userRole.UserId,
                        appRole.Name,
                        appRole.TenantId,
                        appRole.IsSystemDefined
                    })
                .Where(userRole => userRole.Name != null)
                .ToListAsync(cancellationToken);

            var tenantIdsByUserId = pageUsers.ToDictionary(user => user.Id, user => user.TenantId);
            rolesByUserId = roleRows
                .GroupBy(userRole => userRole.UserId)
                .ToDictionary(
                    group => group.Key,
                    group => FilterRolesForCurrentTenant(
                            group.Select(row => (row.Name!, row.TenantId, row.IsSystemDefined)),
                            tenantId,
                            tenantIdsByUserId.GetValueOrDefault(group.Key))
                        .ToList());
        }

        IReadOnlyList<UserWithRoles> usersResult = pageUsers
            .Select(user =>
            {
                return new UserWithRoles
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    IsVerified = IsVerified(user.AuthProvider, user.EmailConfirmed),
                    AuthProvider = user.AuthProvider,
                    IsExternal = user.AuthProvider != AuthProviders.Endatix,
                    IsLockedOut = IsLockedOut(user.LockoutEnd),
                    DisplayName = user.DisplayName,
                    LastLoginAt = user.LastLoginAt,
                    Roles = user.AuthProvider != AuthProviders.Endatix
                        ? ReadExternalRoles(user.Id, user.ExternalRolesJson)
                        : rolesByUserId.GetValueOrDefault(user.Id) ?? []
                };
            })
            .ToList();

        var paged = Paged<UserWithRoles>.FromSkipAndTake(
            effectiveSkip,
            take,
            totalRecords,
            usersResult);

        return Result.Success(paged);
    }

    private static IOrderedQueryable<AppUser> ApplyUserListOrdering(
        IQueryable<AppUser> query,
        SortRequest<UserListSortBy>? sort)
    {
        // Preserve v1 default when sort is omitted: UserName then Email ascending.
        if (sort is null)
        {
            return query.OrderBy(user => user.UserName).ThenBy(user => user.Email);
        }

        var sortDescending = sort.IsDescending;
        return sort.Field switch
        {
            UserListSortBy.Email when sortDescending =>
                query.OrderByDescending(user => user.Email).ThenBy(user => user.Id),
            UserListSortBy.Email =>
                query.OrderBy(user => user.Email).ThenBy(user => user.Id),
            UserListSortBy.LastLoginAt when sortDescending =>
                query.OrderByDescending(user => user.LastLoginAt).ThenBy(user => user.Id),
            UserListSortBy.LastLoginAt =>
                query.OrderBy(user => user.LastLoginAt).ThenBy(user => user.Id),
            UserListSortBy.UserName when sortDescending =>
                query.OrderByDescending(user => user.UserName).ThenBy(user => user.Id),
            _ =>
                query.OrderBy(user => user.UserName).ThenBy(user => user.Id),
        };
    }

    private static IQueryable<AppUser> ApplyStatusFilter(IQueryable<AppUser> query, string? status)
    {
        return status switch
        {
            "active" => query.Where(user =>
                (user.EmailConfirmed || user.AuthProvider != AuthProviders.Endatix) &&
                (user.LockoutEnd == null || user.LockoutEnd <= DateTimeOffset.UtcNow)),
            "pending" => query.Where(user => user.AuthProvider == AuthProviders.Endatix && !user.EmailConfirmed),
            "locked" => query.Where(user => user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow),
            _ => query
        };
    }

    private IQueryable<AppUser> ApplyRoleFilter(IQueryable<AppUser> query, string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return query;
        }

        var normalizedRole = NormalizeRoleName(role);
        return query.Where(user =>
            (user.AuthProvider != AuthProviders.Endatix &&
                user.ExternalRolesJson != null &&
                user.ExternalRolesJson.ToUpper().Contains(normalizedRole)) ||
            identityDbContext.UserRoles.Any(userRole =>
                userRole.UserId == user.Id &&
                identityDbContext.Roles.Any(appRole =>
                    appRole.Id == userRole.RoleId &&
                    appRole.NormalizedName == normalizedRole)));
    }

    private IQueryable<AppUser> ApplySearchFilter(IQueryable<AppUser> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var trimmedSearch = search.Trim();
        var userNameMatches = substringLikeFilter.WherePropertyMatchesLikeSubstring(
            query,
            nameof(AppUser.UserName),
            trimmedSearch);
        var emailMatches = substringLikeFilter.WherePropertyMatchesLikeSubstring(
            query,
            nameof(AppUser.Email),
            trimmedSearch);
        var displayNameMatches = substringLikeFilter.WherePropertyMatchesLikeSubstring(
            query,
            nameof(AppUser.DisplayName),
            trimmedSearch);

        return userNameMatches.Union(emailMatches).Union(displayNameMatches);
    }

    /// <inheritdoc />
    public async Task<Result<UserWithRoles>> GetUserWithRolesAsync(long userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Result.NotFound();
        }

        var tenantId = tenantContext.TenantId;
        var user = await identityDbContext.Users
            .AsNoTracking()
            .MembersOf(identityDbContext.UserRoles, identityDbContext.Roles, tenantId)
            .Where(user =>
                user.Id == userId &&
                user.UserName != null &&
                user.UserName != string.Empty)
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return Result.NotFound();
        }

        var roles = user.IsExternal
            ? ReadExternalRoles(user.Id, user.ExternalRolesJson)
            : await GetAssignedRoleNamesAsync(user.Id, user.TenantId, tenantId, cancellationToken);

        UserWithRoles userWithRoles = new()
        {
            Id = user.Id,
            UserName = user.UserName!,
            Email = user.Email,
            IsVerified = user.IsVerified,
            AuthProvider = user.AuthProvider,
            IsExternal = user.IsExternal,
            IsLockedOut = IsLockedOut(user.LockoutEnd),
            DisplayName = user.DisplayName,
            LastLoginAt = user.LastLoginAt,
            Roles = roles
        };

        return Result.Success(userWithRoles);
    }

    private static int NormalizeSkip(int skip, int take, long totalRecords)
    {
        if (totalRecords == 0 || skip < totalRecords)
        {
            return skip;
        }

        var totalPages = (totalRecords + take - 1) / take;
        return (int)((totalPages - 1) * take);
    }

    /// <inheritdoc />
    public async Task<Result<User>> GetUserAsync(ClaimsPrincipal claimsPrincipal, CancellationToken cancellationToken = default)
    {
        if (claimsPrincipal == null)
        {
            return Result.NotFound();
        }

        var user = await userManager.GetUserAsync(claimsPrincipal);
        if (user == null)
        {
            return Result.NotFound();
        }

        return Result.Success(user.ToUserEntity());
    }

    /// <inheritdoc />
    public async Task<Result<User>> GetUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Result.NotFound();
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result.NotFound();
        }

        return Result.Success(user.ToUserEntity());
    }

    /// <inheritdoc />
    public async Task<Result<User>> GetUserAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.NotFound();
        }

        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user == null)
        {
            return Result.NotFound();
        }

        return Result.Success(user.ToUserEntity());
    }

    /// <inheritdoc />
    public async Task<Result> RemoveUserAccessAsync(long userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Result.NotFound();
        }

        var tenantId = tenantContext.TenantId;
        var user = await FindCurrentTenantUserAsync(userId, tenantId, cancellationToken);
        if (user is null)
        {
            return Result.NotFound();
        }

        var removalGuard = await EnsureTenantAccessRemovalAllowedAsync(user, tenantId, cancellationToken);
        if (!removalGuard.IsSuccess)
        {
            return removalGuard;
        }

        return await RemoveTenantAccessAsync(user, tenantId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result> CancelUserInviteAsync(long userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Result.NotFound();
        }

        var tenantId = tenantContext.TenantId;
        var user = await FindCurrentTenantUserAsync(userId, tenantId, cancellationToken);
        if (user is null)
        {
            return Result.NotFound();
        }

        if (user.IsVerified)
        {
            return Result.Invalid(new ValidationError("Cannot cancel an invite after the user has activated their account."));
        }

        var removalGuard = await EnsureTenantAccessRemovalAllowedAsync(user, tenantId, cancellationToken);
        if (!removalGuard.IsSuccess)
        {
            return removalGuard;
        }

        var invalidateResult = await emailVerificationService.InvalidateVerificationTokensAsync(user.Id, cancellationToken);
        if (!invalidateResult.IsSuccess)
        {
            return invalidateResult;
        }

        return await RemoveTenantAccessAsync(user, tenantId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result> LockoutUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Result.NotFound();
        }

        if (IsCurrentUser(userId))
        {
            return Result.Forbidden(SelfLockoutForbiddenMessage);
        }

        var tenantId = tenantContext.TenantId;
        var user = await FindCurrentTenantUserAsync(userId, tenantId, cancellationToken);
        if (user is null)
        {
            return Result.NotFound();
        }

        var enableLockoutResult = await userManager.SetLockoutEnabledAsync(user, true);
        if (!enableLockoutResult.Succeeded)
        {
            return ToErrorResult(enableLockoutResult);
        }

        var lockoutResult = await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        if (!lockoutResult.Succeeded)
        {
            return ToErrorResult(lockoutResult);
        }

        await authorizationCache.InvalidateAsync(user.Id.ToString(), tenantId, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> UnlockUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Result.NotFound();
        }

        var tenantId = tenantContext.TenantId;
        var user = await FindCurrentTenantUserAsync(userId, tenantId, cancellationToken);
        if (user is null)
        {
            return Result.NotFound();
        }

        var unlockResult = await userManager.SetLockoutEndDateAsync(user, null);
        if (!unlockResult.Succeeded)
        {
            return ToErrorResult(unlockResult);
        }

        await authorizationCache.InvalidateAsync(user.Id.ToString(), tenantId, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<long>>> ListMembershipTenantIdsAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Result<IReadOnlyList<long>>.NotFound();
        }

        var tenantIds = await TenantMembershipQueries
            .MembershipTenantIds(
                identityDbContext.Users.AsNoTracking(),
                identityDbContext.UserRoles.AsNoTracking(),
                identityDbContext.Roles.AsNoTracking(),
                userId)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<long>>(tenantIds);
    }

    /// <inheritdoc />
    public async Task<Result<User>> SetActiveTenantAsync(
        long userId,
        long tenantId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || tenantId <= 0)
        {
            return Result<User>.NotFound();
        }

        var user = await identityDbContext.Users
            .MembersOf(identityDbContext.UserRoles, identityDbContext.Roles, tenantId)
            .FirstOrDefaultAsync(appUser => appUser.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result<User>.Forbidden("User does not belong to the requested tenant.");
        }

        if (user.TenantId != tenantId)
        {
            user.TenantId = tenantId;
            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return ToErrorResult(updateResult).ToErrorResult<User>();
            }
        }

        return Result.Success(user.ToUserEntity());
    }

    private Task<AppUser?> FindCurrentTenantUserAsync(long userId, long tenantId, CancellationToken cancellationToken)
    {
        return identityDbContext.Users
            .MembersOf(identityDbContext.UserRoles, identityDbContext.Roles, tenantId)
            .FirstOrDefaultAsync(appUser => appUser.Id == userId, cancellationToken);
    }

    private async Task<Result> EnsureTenantAccessRemovalAllowedAsync(AppUser user, long tenantId, CancellationToken cancellationToken)
    {
        if (IsCurrentUser(user.Id))
        {
            return Result.Forbidden(SelfRemovalForbiddenMessage);
        }

        if (await RemovingUserWouldLeaveTenantWithoutActiveAdminAsync(user, tenantId, cancellationToken))
        {
            return Result.Conflict(LastTenantAdminRemovalForbiddenMessage);
        }

        return Result.Success();
    }

    private bool IsCurrentUser(long userId)
    {
        var currentUserId = userContext.GetCurrentUserId();
        return long.TryParse(currentUserId, out var currentUserIdValue) && currentUserIdValue == userId;
    }

    private async Task<bool> RemovingUserWouldLeaveTenantWithoutActiveAdminAsync(
        AppUser user,
        long tenantId,
        CancellationToken cancellationToken)
    {
        if (!user.IsVerified)
        {
            return false;
        }

        var userHasTenantAdminRole = await UserHasTenantAdminRoleAsync(user.Id, tenantId, cancellationToken);
        if (!userHasTenantAdminRole)
        {
            return false;
        }

        return !await TenantHasAnotherActiveAdminAsync(user.Id, tenantId, cancellationToken);
    }

    private Task<bool> UserHasTenantAdminRoleAsync(long userId, long tenantId, CancellationToken cancellationToken)
    {
        return identityDbContext.UserRoles
            .Where(userRole => userRole.UserId == userId)
            .Join(
                TenantAdminRoles(tenantId),
                userRole => userRole.RoleId,
                role => role.Id,
                (_, _) => true)
            .AnyAsync(cancellationToken);
    }

    private Task<bool> TenantHasAnotherActiveAdminAsync(long removedUserId, long tenantId, CancellationToken cancellationToken)
    {
        return identityDbContext.UserRoles
            .Where(userRole => userRole.UserId != removedUserId)
            .Join(
                TenantAdminRoles(tenantId),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, _) => userRole.UserId)
            .Join(
                identityDbContext.Users.MembersOf(
                    identityDbContext.UserRoles,
                    identityDbContext.Roles,
                    tenantId)
                    .Where(user =>
                    user.EmailConfirmed || user.AuthProvider != AuthProviders.Endatix),
                userId => userId,
                user => user.Id,
                (_, _) => true)
            .AnyAsync(cancellationToken);
    }

    private IQueryable<AppRole> TenantAdminRoles(long tenantId)
    {
        var normalizedAdminRoleName = NormalizeRoleName(SystemRole.Admin.Name);

        return identityDbContext.Roles.Where(role =>
            (role.TenantId == tenantId || (role.IsSystemDefined && role.TenantId <= 0)) &&
            (role.NormalizedName == normalizedAdminRoleName || role.Name == SystemRole.Admin.Name));
    }

    private async Task<Result> RemoveTenantAccessAsync(AppUser user, long tenantId, CancellationToken cancellationToken)
    {
        var remainingTenantIds = await TenantMembershipQueries
            .MembershipTenantIds(
                identityDbContext.Users,
                identityDbContext.UserRoles,
                identityDbContext.Roles,
                user.Id)
            .Where(id => id != tenantId)
            .ToListAsync(cancellationToken);

        var tenantScopedRoles = await identityDbContext.UserRoles
            .TenantScopedRoleAssignments(identityDbContext.Roles, user.Id, tenantId)
            .ToListAsync(cancellationToken);
        identityDbContext.UserRoles.RemoveRange(tenantScopedRoles);

        if (remainingTenantIds.Count == 0)
        {
            var sharedRoles = await identityDbContext.UserRoles
                .SharedNonPlatformSystemAssignments(identityDbContext.Roles, user.Id)
                .ToListAsync(cancellationToken);
            identityDbContext.UserRoles.RemoveRange(sharedRoles);
            user.TenantId = 0;
        }
        else if (user.TenantId == tenantId)
        {
            user.TenantId = remainingTenantIds[0];
        }

        await identityDbContext.SaveChangesAsync(cancellationToken);
        await authorizationCache.InvalidateAsync(user.Id.ToString(), tenantId, cancellationToken);
        return Result.Success();
    }


    private static string NormalizeRoleName(string roleName)
    {
        return roleName.Trim().ToUpperInvariant();
    }

    private async Task<IReadOnlyList<string>> GetAssignedRoleNamesAsync(
        long userId,
        long homeTenantId,
        long tenantId,
        CancellationToken cancellationToken)
    {
        var roleRows = await identityDbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == userId)
            .Join(
                identityDbContext.Roles.AsNoTracking(),
                userRole => userRole.RoleId,
                appRole => appRole.Id,
                (_, appRole) => new
                {
                    appRole.Name,
                    appRole.TenantId,
                    appRole.IsSystemDefined
                })
            .Where(role => role.Name != null)
            .ToListAsync(cancellationToken);

        return FilterRolesForCurrentTenant(
            roleRows.Select(role => (role.Name!, role.TenantId, role.IsSystemDefined)),
            tenantId,
            homeTenantId);
    }

    internal static IReadOnlyList<string> FilterRolesForCurrentTenant(
        IEnumerable<(string Name, long TenantId, bool IsSystemDefined)> roles,
        long tenantId,
        long homeTenantId)
    {
        var tenantScoped = roles
            .Where(role => role.TenantId == tenantId)
            .Select(role => role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();

        if (tenantScoped.Count > 0)
        {
            return tenantScoped;
        }

        if (homeTenantId != tenantId)
        {
            return [];
        }

        return roles
            .Where(role =>
                role.IsSystemDefined
                && role.TenantId <= 0
                && role.Name != SystemRole.PlatformAdmin.Name)
            .Select(role => role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();
    }

    private IReadOnlyList<string> ReadExternalRoles(long userId, string? externalRolesJson)
    {
        if (string.IsNullOrWhiteSpace(externalRolesJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(externalRolesJson)?
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(role => role)
                .ToList() ?? [];
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "Failed to deserialize external roles for user {UserId}. ExternalRolesJson: {ExternalRolesJson}",
                userId,
                externalRolesJson);

            return [];
        }
    }

    private static bool IsLockedOut(DateTimeOffset? lockoutEnd)
    {
        return lockoutEnd is not null && lockoutEnd > DateTimeOffset.UtcNow;
    }

    private static bool IsVerified(string authProvider, bool emailConfirmed)
    {
        return authProvider != AuthProviders.Endatix || emailConfirmed;
    }

    private static Result ToErrorResult(IdentityResult result)
    {
        return Result.Error(new ErrorList(result.Errors.Select(error => error.Description)));
    }
}
