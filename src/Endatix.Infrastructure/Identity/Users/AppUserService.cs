using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Identity;
using Endatix.Infrastructure.Data.Querying;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Identity.Users;

/// <summary>
/// Implements the user service by leveraging the integration with ASP.NET Core Identity including the registered <see cref="AppUser" /> persisted object />
/// </summary>
public sealed class AppUserService(
    UserManager<AppUser> userManager,
    ITenantContext tenantContext,
    AppIdentityDbContext identityDbContext,
    IEmailVerificationService emailVerificationService,
    IRelationalSubstringLikeFilter substringLikeFilter) : IUserService
{
    /// <inheritdoc />
    public async Task<Result<Paged<UserWithRoles>>> ListUsersAsync(
        int skip,
        int take,
        string? search,
        string? role,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var pagingGuard = ValidatePaging(skip, take);
        if (!pagingGuard.IsSuccess)
        {
            return pagingGuard.ToErrorResult<Paged<UserWithRoles>>();
        }

        var tenantId = tenantContext.TenantId;

        var filteredUsers = identityDbContext.Users
            .AsNoTracking()
            .Where(user => user.TenantId == tenantId)
            .Where(user =>
                user.UserName != null &&
                user.UserName != string.Empty &&
                user.Email != null &&
                user.Email != string.Empty);

        filteredUsers = ApplyStatusFilter(filteredUsers, status);
        filteredUsers = ApplyRoleFilter(filteredUsers, role);
        filteredUsers = ApplySearchFilter(filteredUsers, search);

        var totalRecords = await filteredUsers.CountAsync(cancellationToken);
        var effectiveSkip = NormalizeSkip(skip, take, totalRecords);

        var pageUsers = await filteredUsers
            .OrderBy(user => user.UserName)
            .ThenBy(user => user.Email)
            .Skip(effectiveSkip)
            .Take(take)
            .Select(user => new
            {
                user.Id,
                UserName = user.UserName!,
                Email = user.Email!,
                user.EmailConfirmed
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
                        appRole.Name
                    })
                .Where(userRole => userRole.Name != null)
                .ToListAsync(cancellationToken);

            rolesByUserId = roleRows
                .GroupBy(userRole => userRole.UserId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(userRole => userRole.Name!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(roleName => roleName)
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
                    IsVerified = user.EmailConfirmed,
                    Roles = rolesByUserId.GetValueOrDefault(user.Id) ?? []
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

    private IQueryable<AppUser> ApplyStatusFilter(IQueryable<AppUser> query, string? status)
    {
        return status switch
        {
            "active" => query.Where(user => user.EmailConfirmed),
            "pending" => query.Where(user => !user.EmailConfirmed),
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

        return userNameMatches.Union(emailMatches);
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

        if (user.EmailConfirmed)
        {
            return Result.Invalid(new ValidationError("Cannot cancel an invite after the user has activated their account."));
        }

        var invalidateResult = await emailVerificationService.InvalidateVerificationTokensAsync(user.Id, cancellationToken);
        if (!invalidateResult.IsSuccess)
        {
            return invalidateResult;
        }

        return await RemoveTenantAccessAsync(user, tenantId, cancellationToken);
    }

    private Task<AppUser?> FindCurrentTenantUserAsync(long userId, long tenantId, CancellationToken cancellationToken)
    {
        return identityDbContext.Users
            .FirstOrDefaultAsync(appUser => appUser.Id == userId && appUser.TenantId == tenantId, cancellationToken);
    }

    private async Task<Result> RemoveTenantAccessAsync(AppUser user, long tenantId, CancellationToken cancellationToken)
    {
        var userRoles = await identityDbContext.UserRoles
            .CurrentTenantRoleAssignments(identityDbContext.Roles, user.Id, tenantId)
            .ToListAsync(cancellationToken);
        identityDbContext.UserRoles.RemoveRange(userRoles);

        user.TenantId = 0;
        await identityDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static Result ValidatePaging(int skip, int take)
    {
        if (skip < 0)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(skip),
                ErrorMessage = "Skip must be greater than or equal to zero."
            });
        }

        if (take > 0)
        {
            return Result.Success();
        }

        return Result.Invalid(new ValidationError
        {
            Identifier = nameof(take),
            ErrorMessage = "Take must be greater than zero."
        });
    }

    private static string NormalizeRoleName(string roleName)
    {
        return roleName.Trim().ToUpperInvariant();
    }
}

internal static class AppUserServiceQueryExtensions
{
    internal static IQueryable<IdentityUserRole<long>> CurrentTenantRoleAssignments(
        this IQueryable<IdentityUserRole<long>> userRoles,
        IQueryable<AppRole> roles,
        long userId,
        long tenantId)
    {
        return userRoles
            .Join(
                roles.Where(role =>
                    role.TenantId == tenantId ||
                    (role.IsSystemDefined && role.TenantId <= 0 && role.Name != SystemRole.PlatformAdmin.Name)),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, _) => userRole)
            .Where(userRole => userRole.UserId == userId);
    }
}
