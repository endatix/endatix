using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Querying;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Features.PlatformAdmin.Common;

/// <summary>
/// Shared EF read logic for platform-admin user list projections.
/// </summary>
public sealed class PlatformAdminUserListing(
    AppIdentityDbContext identityDbContext,
    AppDbContext appDbContext,
    IRelationalSubstringLikeFilter substringLikeFilter) : IPlatformAdminUserListing
{
    public async Task<long?> GetPlatformAdminRoleIdAsync(CancellationToken cancellationToken)
    {
        var normalizedRoleName = SystemRole.PlatformAdmin.Name.ToUpperInvariant();

        return await identityDbContext.Roles
            .AsNoTracking()
            .Where(role => role.IsActive && role.NormalizedName == normalizedRoleName)
            .Select(role => (long?)role.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<Paged<PlatformAdminUserListItem>>> ListAsync(
        int page,
        int pageSize,
        string? search,
        long? platformAdminRoleId,
        PlatformAdminUserScopeFilter scopeFilter,
        CancellationToken cancellationToken,
        bool prioritizeExternalPlatformAdminRole = false)
    {
        var normalizedPage = Math.Max(page, PagedRequestLimits.DEFAULT_PAGE);
        var normalizedPageSize = NormalizePageSize(pageSize);
        var skip = (normalizedPage - 1) * normalizedPageSize;

        var usersQuery = BuildTenantUsersQuery();
        usersQuery = PlatformAdminUserRoleScope.Apply(
            usersQuery,
            identityDbContext.UserRoles.AsNoTracking(),
            platformAdminRoleId,
            scopeFilter);
        usersQuery = ApplySearch(usersQuery, search);

        var totalRecords = await usersQuery.CountAsync(cancellationToken);

        var users = await OrderUsers(usersQuery, prioritizeExternalPlatformAdminRole)
            .Skip(skip)
            .Take(normalizedPageSize)
            .Select(user => new UserRow(
                user.Id,
                user.TenantId,
                user.UserName ?? string.Empty,
                user.Email,
                user.DisplayName,
                user.AuthProvider,
                user.EmailConfirmed,
                user.LockoutEnd,
                user.LastLoginAt,
                user.ExternalRolesJson))
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
        {
            return Result.Success(Paged<PlatformAdminUserListItem>.FromSkipAndTake(
                skip,
                normalizedPageSize,
                totalRecords,
                []));
        }

        var userIds = users.ConvertAll(user => user.Id);
        var tenantIds = users.Select(user => user.TenantId).Distinct().ToList();

        var rolesTask =
            LoadRolesByUserIdAsync(userIds, cancellationToken);
        var tenantNamesTask =
            LoadTenantNamesByIdAsync(tenantIds, cancellationToken);

        await Task.WhenAll(rolesTask, tenantNamesTask);

        var rolesByUserId = await rolesTask;
        var tenantNamesById = await tenantNamesTask;

        var items = users
            .ConvertAll(user => MapToListItem(user, tenantNamesById, rolesByUserId));

        return Result.Success(Paged<PlatformAdminUserListItem>.FromSkipAndTake(
            skip,
            normalizedPageSize,
            totalRecords,
            items));
    }

    private IQueryable<AppUser> BuildTenantUsersQuery() =>
        identityDbContext.Users
            .AsNoTracking()
            .Where(user => user.TenantId > 0);

    private IQueryable<AppUser> ApplySearch(IQueryable<AppUser> usersQuery, string? search)
    {
        var trimmedSearch = NormalizeSearch(search);
        if (trimmedSearch is null)
        {
            return usersQuery;
        }

        var userNameMatches = substringLikeFilter.WherePropertyMatchesLikeSubstring(
            usersQuery,
            nameof(AppUser.UserName),
            trimmedSearch);
        var emailMatches = substringLikeFilter.WherePropertyMatchesLikeSubstring(
            usersQuery,
            nameof(AppUser.Email),
            trimmedSearch);
        var displayNameMatches = substringLikeFilter.WherePropertyMatchesLikeSubstring(
            usersQuery,
            nameof(AppUser.DisplayName),
            trimmedSearch);

        return userNameMatches.Union(emailMatches).Union(displayNameMatches);
    }

    private static string? NormalizeSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var trimmed = search.Trim();
        if (trimmed.Length > PagedRequestLimits.MAX_SEARCH_LENGTH)
        {
            trimmed = trimmed[..PagedRequestLimits.MAX_SEARCH_LENGTH];
        }

        return trimmed;
    }

    private static IOrderedQueryable<AppUser> OrderUsers(
        IQueryable<AppUser> usersQuery,
        bool prioritizeExternalPlatformAdminRole)
    {
        var quotedRoleName = PlatformAdminExternalRoleReader.QuotedPlatformAdminRoleName;

        if (!prioritizeExternalPlatformAdminRole)
        {
            return usersQuery
                .OrderBy(user => user.Email)
                .ThenBy(user => user.UserName);
        }

        return usersQuery
            .OrderByDescending(user =>
                user.AuthProvider != AuthProviders.Endatix &&
                user.ExternalRolesJson != null &&
                user.ExternalRolesJson.Contains(quotedRoleName))
            .ThenBy(user => user.Email)
            .ThenBy(user => user.UserName);
    }

    private static PlatformAdminUserListItem MapToListItem(
        UserRow user,
        IReadOnlyDictionary<long, string> tenantNamesById,
        IReadOnlyDictionary<long, IReadOnlyList<string>> rolesByUserId)
    {
        var isExternal = !string.Equals(
            user.AuthProvider,
            AuthProviders.Endatix,
            StringComparison.OrdinalIgnoreCase);

        return new PlatformAdminUserListItem(
            user.Id,
            user.TenantId,
            tenantNamesById.GetValueOrDefault(user.TenantId),
            user.UserName,
            user.Email,
            user.DisplayName,
            user.AuthProvider,
            isExternal,
            isExternal || user.EmailConfirmed,
            user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow,
            user.LastLoginAt,
            PlatformAdminExternalRoleReader.HasPlatformAdminRole(user.ExternalRolesJson),
            rolesByUserId.GetValueOrDefault(user.Id) ?? []);
    }

    private async Task<Dictionary<long, IReadOnlyList<string>>> LoadRolesByUserIdAsync(
        IReadOnlyCollection<long> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var roleRows = await identityDbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userIds.Contains(userRole.UserId))
            .Join(
                identityDbContext.Roles.AsNoTracking().Where(role => role.Name != null),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Name })
            .ToListAsync(cancellationToken);

        return roleRows
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(row => row.Name!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(roleName => roleName, StringComparer.OrdinalIgnoreCase)
                    .ToList());
    }

    private async Task<Dictionary<long, string>> LoadTenantNamesByIdAsync(
        IReadOnlyCollection<long> tenantIds,
        CancellationToken cancellationToken)
    {
        if (tenantIds.Count == 0)
        {
            return [];
        }

        return await appDbContext.Set<Endatix.Core.Entities.Tenant>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(tenant => tenantIds.Contains(tenant.Id))
            .ToDictionaryAsync(tenant => tenant.Id, tenant => tenant.Name, cancellationToken);
    }

    private static int NormalizePageSize(int pageSize) =>
        Math.Clamp(pageSize, PagedRequestLimits.MIN_PAGE_SIZE, PagedRequestLimits.MAX_PAGE_SIZE);

    private sealed record UserRow(
        long Id,
        long TenantId,
        string UserName,
        string? Email,
        string? DisplayName,
        string AuthProvider,
        bool EmailConfirmed,
        DateTimeOffset? LockoutEnd,
        DateTimeOffset? LastLoginAt,
        string? ExternalRolesJson);
}
