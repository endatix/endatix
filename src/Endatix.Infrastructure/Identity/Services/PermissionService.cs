using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Infrastructure.Identity.Services;

/// <summary>
/// High-performance permission service with multi-level caching and optimization.
/// Uses hybrid caching, in-memory fast cache, and batch operations for enterprise performance.
/// </summary>
internal sealed class PermissionService : IPermissionService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppIdentityDbContext _identityContext;
    private readonly HybridCache _hybridCache;
    private readonly ITenantContext _tenantContext;

    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<PermissionService> _logger;

    private static readonly TimeSpan _hybridCacheExpiration = TimeSpan.FromMinutes(15);

    public PermissionService(
        UserManager<AppUser> userManager,
        AppIdentityDbContext identityContext,
        HybridCache hybridCache,
        ITenantContext tenantContext,
        IDateTimeProvider dateTimeProvider,
        ILogger<PermissionService> logger)
    {
        _userManager = userManager;
        _identityContext = identityContext;
        _hybridCache = hybridCache;
        _tenantContext = tenantContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<bool>> HasPermissionAsync(long userId, string permission, CancellationToken cancellationToken = default)
    {
        var permissionsInfoResult = await GetUserPermissionsInfoAsync(userId, cancellationToken);
        if (!permissionsInfoResult.IsSuccess)
        {
            return Result.Error("Failed to get user permissions info");
        }

        var permissionInfo = permissionsInfoResult.Value;

        if (permissionInfo.IsAdmin)
        {
            return Result.Success(true);
        }

        return Result.Success(permissionInfo.Permissions.Contains(permission));
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, bool>>> HasPermissionsAsync(long userId, IEnumerable<string> permissions, CancellationToken cancellationToken = default)
    {
        var permissionsInfoResult = await GetUserPermissionsInfoAsync(userId, cancellationToken);
        if (!permissionsInfoResult.IsSuccess)
        {
            return Result.Error("Failed to get user permissions info");
        }

        var permissionInfo = permissionsInfoResult.Value;

        if (permissionInfo.IsAdmin)
        {
            return Result.Success(permissions.ToDictionary(p => p, _ => true));
        }

        return Result.Success(permissions.ToDictionary(p => p, permissionInfo.Permissions.Contains));
    }

    /// <inheritdoc />
    public async Task<Result<AuthorizationData>> GetUserPermissionsInfoAsync(long userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GetUserPermissionsCacheKey(userId, _tenantContext.TenantId);

            var permissionsInfo = await _hybridCache.GetOrCreateAsync(
                cacheKey,
                async cancel => await GetUserPermissionsInfoInternalAsync(userId, cancellationToken),
                new HybridCacheEntryOptions { Expiration = _hybridCacheExpiration },
                tags: ["usr_rbac:all", $"usr_rbac:{_tenantContext.TenantId}"],
                cancellationToken: cancellationToken);

            return Result.Success(permissionsInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role info for user {UserId}", userId);
            return Result.Error("Failed to retrieve user role information");
        }
    }


    /// <inheritdoc />
    public async Task InvalidateUserPermissionCacheAsync(long userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        var userPermissionKey = GetUserPermissionsCacheKey(userId, tenantId);

        await _hybridCache.RemoveAsync(userPermissionKey);
    }

    /// <inheritdoc />

    public async Task<Result<bool>> IsUserAdminAsync(long userId, CancellationToken cancellationToken = default)
    {
        var permissionsInfoResult = await GetUserPermissionsInfoAsync(userId, cancellationToken);
        if (!permissionsInfoResult.IsSuccess)
        {
            return Result.Error("Failed to get user permissions info");
        }

        var permissionInfo = permissionsInfoResult.Value;

        return Result.Success(permissionInfo.IsAdmin);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> IsUserPlatformAdminAsync(long userId, CancellationToken cancellationToken = default)
    {
        var permissionsInfoResult = await GetUserPermissionsInfoAsync(userId, cancellationToken);
        if (!permissionsInfoResult.IsSuccess)
        {
            return Result.Error("Failed to get user permissions info");
        }

        var permissionInfo = permissionsInfoResult.Value;

        return Result.Success(permissionInfo.Roles.Contains(SystemRole.PlatformAdmin.Name));
    }

    /// <inheritdoc />
    public async Task<Result> ValidateAccessAsync(string? userId, string requiredPermission, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || !long.TryParse(userId, out var parsedUserId))
        {
            return Result.Unauthorized("Authentication required to access this resource.");
        }

        var isAdminResult = await IsUserAdminAsync(parsedUserId, cancellationToken);
        if (isAdminResult.IsSuccess && isAdminResult.Value)
        {
            return Result.Success();
        }

        var hasPermissionResult = await HasPermissionAsync(parsedUserId, requiredPermission, cancellationToken);
        if (hasPermissionResult.IsSuccess && hasPermissionResult.Value)
        {
            return Result.Success();
        }

        return Result.Forbidden($"Permission '{requiredPermission}' required to access this resource.");
    }


    private async Task<AuthorizationData> GetUserPermissionsInfoInternalAsync(long userId, CancellationToken cancellationToken = default)
    {
        var utcNow = _dateTimeProvider.Now.UtcDateTime;

        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return new AuthorizationData
                {
                    UserId = userId,
                    TenantId = _tenantContext.TenantId,
                    Roles = Array.Empty<string>(),
                    Permissions = [Actions.Access.Authenticated],
                    IsAdmin = false,
                    CachedAt = utcNow,
                    CacheExpiresIn = _hybridCacheExpiration,
                    ETag = GenerateETag(userId, Array.Empty<string>(), Array.Empty<string>()),
                    FromCache = false
                };
            }

            var userRoleIds = _identityContext.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.RoleId);

            var userRoles = await _identityContext.Roles
                .Where(r => r.IsActive && userRoleIds.Contains(r.Id))
                .Include(r => r.RolePermissions.Where(rp => rp.IsActive && (rp.ExpiresAt == null || rp.ExpiresAt > utcNow)))
                .ThenInclude(rp => rp.Permission)
                .AsSplitQuery()
                .AsNoTracking()
                .ToListAsync(cancellationToken);


            var assignedRoles = userRoles
                .Select(r => r.Name!)
                .ToArray() ?? Array.Empty<string>();

            var assignedPermissions = userRoles
                .SelectMany(r => r.RolePermissions.Select(rp => rp.Permission.Name))
                .Distinct()
                .ToArray();

            return new AuthorizationData
            {
                UserId = userId,
                TenantId = user.TenantId,
                Roles = assignedRoles,
                Permissions = [Actions.Access.Authenticated, .. assignedPermissions],
                IsAdmin = assignedRoles.Contains(SystemRole.Admin.Name) || assignedRoles.Contains(SystemRole.PlatformAdmin.Name),
                CachedAt = DateTime.UtcNow,
                CacheExpiresIn = _hybridCacheExpiration,
                ETag = GenerateETag(userId, assignedRoles, assignedPermissions),
                FromCache = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user permissions info for user {UserId}", userId);
            return new AuthorizationData
            {
                UserId = userId,
                TenantId = _tenantContext.TenantId,
                Roles = Array.Empty<string>(),
                Permissions = [Actions.Access.Authenticated],
                IsAdmin = false,
                CachedAt = utcNow,
                CacheExpiresIn = _hybridCacheExpiration,
                ETag = GenerateETag(userId, Array.Empty<string>(), Array.Empty<string>()),
                FromCache = false
            };
        }
    }


    /// <summary>
    /// Generates an ETag for the user permissions.
    /// </summary>
    /// <param name="userId">The user ID to generate the ETag for.</param>
    /// <param name="roles">The roles to generate the ETag for.</param>
    /// <param name="permissions">The permissions to generate the ETag for.</param>
    /// <returns>The ETag for the user permissions.</returns>
    private static string GenerateETag(long userId, IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var content = $"{userId}:{string.Join(",", roles)}:{string.Join(",", permissions)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash)[..8]; // Short ETag
    }

    private static string GetUserPermissionsCacheKey(long userId, long tenantId) => $"usr_rbac:{userId}:{tenantId}";
}
