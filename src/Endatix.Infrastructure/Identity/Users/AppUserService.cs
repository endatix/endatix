using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Identity;
using Endatix.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Identity.Users;

/// <summary>
/// Implements the user service by leveraging the integration with ASP.NET Core Identity including the registered <see cref="AppUser" /> persisted object />
/// </summary>
public class AppUserService(
    UserManager<AppUser> userManager,
    ITenantContext tenantContext,
    AppIdentityDbContext identityDbContext) : IUserService
{
    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<UserWithRoles>>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.TenantId;

        var usersWithRoles = await identityDbContext
            .Users
            .Where(user => user.TenantId == tenantId)
            .LeftJoin(
                identityDbContext.UserRoles,
                user => user.Id,
                userRole => userRole.UserId,
                (user, userRole) => new
                {
                    user.Id,
                    user.UserName,
                    user.Email,
                    user.EmailConfirmed,
                    RoleId = userRole != null ? userRole.RoleId : default
                })
            .LeftJoin(
                identityDbContext.Roles,
                userRoles => userRoles.RoleId,
                role => role.Id,
                (userRoles, role) => new
                {
                    userRoles.Id,
                    userRoles.UserName,
                    userRoles.Email,
                    userRoles.EmailConfirmed,
                    RoleName = role != null ? role.Name : default
                })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        IReadOnlyList<UserWithRoles> usersResult = usersWithRoles
            .GroupBy(userRoles => userRoles.Id)
            .Select(userRolesGroup => new UserWithRoles
            {
                Id = userRolesGroup.Key,
                UserName = userRolesGroup.First()?.UserName ?? string.Empty,
                Email = userRolesGroup.First()?.Email ?? string.Empty,
                IsVerified = userRolesGroup.First().EmailConfirmed,
                Roles = userRolesGroup
                            .Where(userRoles => userRoles.RoleName != null)
                            .Select(userRoles => userRoles.RoleName!)
                            .Distinct()
                            .ToList()
            })
            .ToList();

        return Result.Success(usersResult);
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

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return Result.NotFound();
        }

        return Result.Success(user.ToUserEntity());
    }
}
