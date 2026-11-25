using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Identity.Users;

public sealed partial class AppUserService
{
    /// <inheritdoc />
    public async Task<Result<UserWithRoles>> GetUserWithRolesAsync(long userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Result.NotFound();
        }

        var visibleUsers = VisibleUser(userId);
        if (_standing.IsEnforced && !await visibleUsers.AnyAsync(cancellationToken))
        {
            return Result.NotFound();
        }

        return await _standing.RefusalAsync<UserWithRoles>(cancellationToken)
            ?? await ReadUserWithRolesAsync(visibleUsers, cancellationToken);
    }

    private IQueryable<AppUser> VisibleUser(long userId)
    {
        var tenantId = tenantContext.TenantId;
        return identityDbContext.Users
            .AsNoTracking()
            .Where(user =>
                user.Id == userId &&
                user.TenantId == tenantId &&
                user.UserName != null &&
                user.UserName != string.Empty);
    }

    private async Task<Result<UserWithRoles>> ReadUserWithRolesAsync(IQueryable<AppUser> visibleUsers, CancellationToken cancellationToken)
    {
        var user = await visibleUsers.FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return Result.NotFound();
        }

        var roles = user.IsExternal
            ? ReadExternalRoles(user.Id, user.ExternalRolesJson)
            : await GetAssignedRoleNamesAsync(user.Id, cancellationToken);

        return Result.Success(MapUser(user, roles));
    }

    private static UserWithRoles MapUser(AppUser user, IReadOnlyList<string> roles) => new()
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
}
