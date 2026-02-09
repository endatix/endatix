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
            .GroupBy(userRolesGroup => userRolesGroup.Id)
            .Where(userRolesGroup =>
            {
                var user = userRolesGroup.First();
                return !string.IsNullOrWhiteSpace(user.UserName) && !string.IsNullOrWhiteSpace(user.Email);
            })
            .Select(userRolesGroup =>
            {
                var user = userRolesGroup.First();
                return new UserWithRoles
                {
                    Id = userRolesGroup.Key,
                    UserName = user.UserName!,
                    Email = user.Email!,
                    IsVerified = user.EmailConfirmed,
                    Roles = userRolesGroup
                        .Where(userRole => !string.IsNullOrWhiteSpace(userRole.RoleName))
                        .Select(userRole => userRole.RoleName!)
                        .Distinct()
                        .ToList()
                };
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
