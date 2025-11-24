using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Authorization.Data;

/// <summary>
/// Default implementation of <see cref="IAuthorizationDataProvider"/> that uses the user manager and identity database context.
/// </summary>
/// <param name="userManager">The user manager.</param>
/// <param name="identityDbContext">The identity database context.</param>
/// <param name="tenantContext">The tenant context.</param>
/// <param name="dateTimeProvider">The date time provider.</param>
/// <param name="logger">The logger.</param>
internal sealed class DefaultAuthorizationDataProvider(
    UserManager<AppUser> userManager,
    AppIdentityDbContext identityDbContext,
    ITenantContext tenantContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<DefaultAuthorizationDataProvider> logger) : IAuthorizationDataProvider
{
    /// <inheritdoc />
    public async Task<Result<AuthorizationData>> GetAuthorizationDataAsync(long userId, CancellationToken cancellationToken)
    {
        var utcNow = dateTimeProvider.Now.UtcDateTime;

        try
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                var anonymousData = AuthorizationData.ForAuthenticatedUser(
                    userId: userId.ToString(),
                    tenantId: tenantContext.TenantId,
                    roles: [],
                    permissions: []
                    );

                return Result.Success(anonymousData);
            }

            var userRoleIds = identityDbContext.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.RoleId);

            var userRoles = await identityDbContext.Roles
                .Where(r => r.IsActive && userRoleIds.Contains(r.Id))
                .Include(r => r.RolePermissions.Where(rp => rp.IsActive && (rp.
                ExpiresAt == null || rp.ExpiresAt > utcNow)))
                .ThenInclude(rp => rp.Permission)
                .AsSplitQuery()
                .AsNoTracking()
                .ToListAsync(cancellationToken);


            var assignedRoles = userRoles
                .Select(r => r.Name!)
                .ToArray() ?? [];

            var assignedPermissions = userRoles
                .SelectMany(r => r.RolePermissions.Select(rp => rp.Permission.
                Name))
                .Distinct()
                .ToArray();

            var authorizationData = AuthorizationData.ForAuthenticatedUser(
                    userId: userId.ToString(),
                    tenantId: user.TenantId,
                    roles: assignedRoles,
                    permissions: assignedPermissions
            );

            return Result.Success(authorizationData);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting user permissions info for user {UserId}", userId);
            return Result.Error("Failed to get user permissions info from the identity store");
        }
    }
}

