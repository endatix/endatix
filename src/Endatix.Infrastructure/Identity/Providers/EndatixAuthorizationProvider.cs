using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Providers;

public sealed class EndatixAuthorizationProvider(
    AuthProviderRegistry authProviderRegistry,
     UserManager<AppUser> userManager,
        AppIdentityDbContext identityDbContext,
        ITenantContext tenantContext,
        IDateTimeProvider dateTimeProvider,
        ILogger<EndatixAuthorizationProvider> logger) : IAuthorizationProvider
{
    /// <inheritdoc />
    public bool CanHandle(ClaimsPrincipal principal)
    {
        var issuer = principal.GetIssuer();
        if (issuer is null)
        {
            return false;
        }

        var activeProvider = authProviderRegistry
            .GetActiveProviders()
            .FirstOrDefault(provider => provider.CanHandle(issuer, string.Empty));

        return activeProvider is not null && activeProvider is EndatixJwtAuthProvider;
    }

    /// <inheritdoc />
    public async Task<Result<AuthorizationData>> GetAuthorizationDataAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {

        if (!CanHandle(principal))
        {
            return Result.Error("Provider cannot handle the given issuer");
        }

        var userId = principal.GetUserId();
        if (userId is null || !long.TryParse(userId, out var endatixUserId))
        {
            return Result.Error("User ID is required");
        }

        var authorizationData = await GetUserPermissionsInfoInternalAsync(endatixUserId, cancellationToken);

        return Result.Success(authorizationData);
    }

    private async Task<AuthorizationData> GetUserPermissionsInfoInternalAsync(long userId, CancellationToken cancellationToken = default)
    {
        var utcNow = dateTimeProvider.Now.UtcDateTime;

        try
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return AuthorizationData.ForAuthenticatedUser(
                    userId: userId.ToString(),
                    tenantId: tenantContext.TenantId,
                    roles: [],
                    permissions: [Actions.Access.Authenticated],
                    cachedAt: utcNow,
                    cacheExpiresIn: TimeSpan.FromMinutes(15),
                    eTag: GenerateETag(userId.ToString(), [], [])
                    );
            }

            var userRoleIds = identityDbContext.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.RoleId);

            var userRoles = await identityDbContext.Roles
                .Where(r => r.IsActive && userRoleIds.Contains(r.Id))
                .Include(r => r.RolePermissions.Where(rp => rp.IsActive && (rp.ExpiresAt == null || rp.ExpiresAt > utcNow)))
                .ThenInclude(rp => rp.Permission)
                .AsSplitQuery()
                .AsNoTracking()
                .ToListAsync(cancellationToken);


            var assignedRoles = userRoles
                .Select(r => r.Name!)
                .ToArray() ?? [];

            var assignedPermissions = userRoles
                .SelectMany(r => r.RolePermissions.Select(rp => rp.Permission.Name))
                .Distinct()
                .ToArray();

            return AuthorizationData.ForAuthenticatedUser(
                    userId: userId.ToString(),
                    tenantId: user.TenantId,
                    roles: assignedRoles,
                    permissions: [Actions.Access.Authenticated, .. assignedPermissions],
                    cachedAt: utcNow,
                    cacheExpiresIn: TimeSpan.FromMinutes(15),
                    eTag: GenerateETag(userId.ToString(), assignedRoles, assignedPermissions)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting user permissions info for user {UserId}", userId);
            return AuthorizationData.ForAuthenticatedUser(
                userId: userId.ToString(),
                tenantId: tenantContext.TenantId,
                roles: Array.Empty<string>(),
                permissions: [Actions.Access.Authenticated],
                cachedAt: utcNow,
                cacheExpiresIn: TimeSpan.FromMinutes(15),
                eTag: GenerateETag(userId.ToString(), [], [])
            );
        }
    }


    /// <summary>
    /// Generates an ETag for the user permissions.
    /// </summary>
    /// <param name="userId">The user ID to generate the ETag for.</param>
    /// <param name="roles">The roles to generate the ETag for.</param>
    /// <param name="permissions">The permissions to generate the ETag for.</param>
    /// <returns>The ETag for the user permissions.</returns>
    private static string GenerateETag(string userId, IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var content = $"{userId}:{string.Join(",", roles)}:{string.Join(",", permissions)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash)[..8]; // Short ETag
    }
}