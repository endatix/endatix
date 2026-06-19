using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Querying;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Microsoft.AspNetCore.Identity;
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
        SearchablePageRequest paging,
        PlatformAdminUserListCriteria criteria,
        CancellationToken cancellationToken)
    {
        var skip = paging.Paging.Skip;
        var pageSize = paging.Paging.PageSize;

        var usersQuery = BuildTenantUsersQuery();
        if (criteria.TenantId is > 0)
        {
            usersQuery = usersQuery.Where(user => user.TenantId == criteria.TenantId.Value);
        }

        usersQuery = PlatformAdminUserRoleScope.Apply(
            usersQuery,
            identityDbContext.UserRoles.AsNoTracking(),
            criteria.PlatformAdminRoleId,
            criteria.ScopeFilter);
        usersQuery = ApplySearch(usersQuery, paging.Search);

        var totalRecords = await usersQuery.CountAsync(cancellationToken);

        var userRoles = identityDbContext.UserRoles.AsNoTracking();
        var users = await OrderUsers(
                usersQuery,
                userRoles,
                criteria.PlatformAdminRoleId,
                criteria.PrioritizeExternalPlatformAdminRole,
                criteria.PrioritizeLocalPlatformAdminRole)
            .Skip(skip)
            .Take(pageSize)
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
                pageSize,
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
            pageSize,
            totalRecords,
            items));
    }

    private IQueryable<AppUser> BuildTenantUsersQuery() =>
        identityDbContext.Users
            .AsNoTracking()
            .Where(user => user.TenantId > 0);

    private IQueryable<AppUser> ApplySearch(IQueryable<AppUser> usersQuery, string? search)
    {
        if (search is null)
        {
            return usersQuery;
        }

        var userNameMatches = substringLikeFilter.WherePropertyMatchesLikeSubstring(
            usersQuery,
            nameof(AppUser.UserName),
            search);
        var emailMatches = substringLikeFilter.WherePropertyMatchesLikeSubstring(
            usersQuery,
            nameof(AppUser.Email),
            search);
        var displayNameMatches = substringLikeFilter.WherePropertyMatchesLikeSubstring(
            usersQuery,
            nameof(AppUser.DisplayName),
            search);

        return userNameMatches.Union(emailMatches).Union(displayNameMatches);
    }

    private static IOrderedQueryable<AppUser> OrderUsers(
        IQueryable<AppUser> usersQuery,
        IQueryable<IdentityUserRole<long>> userRoles,
        long? platformAdminRoleId,
        bool prioritizeExternalPlatformAdminRole,
        bool prioritizeLocalPlatformAdminRole)
    {
        var quotedRoleName = PlatformAdminExternalRoleReader.QuotedPlatformAdminRoleName;

        if (prioritizeExternalPlatformAdminRole)
        {
            return usersQuery
                .OrderByDescending(user =>
                    user.AuthProvider != AuthProviders.Endatix &&
                    user.ExternalRolesJson != null &&
                    user.ExternalRolesJson.Contains(quotedRoleName))
                .ThenBy(user => user.Email)
                .ThenBy(user => user.UserName);
        }

        if (prioritizeLocalPlatformAdminRole && platformAdminRoleId is not null)
        {
            var platformAdminUserIds = userRoles
                .Where(userRole => userRole.RoleId == platformAdminRoleId.Value)
                .Select(userRole => userRole.UserId);

            return usersQuery
                .OrderByDescending(user => platformAdminUserIds.Contains(user.Id))
                .ThenBy(user => user.Email)
                .ThenBy(user => user.UserName);
        }

        return usersQuery
            .OrderBy(user => user.Email)
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
