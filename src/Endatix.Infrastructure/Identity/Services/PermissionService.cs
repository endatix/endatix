using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Identity.Services;

/// <summary>
/// High-performance permission service with multi-level caching and optimization.
/// Uses hybrid caching, in-memory fast cache, and batch operations for enterprise performance.
/// </summary>
internal sealed class PermissionService : IPermissionService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<AppRole> _roleManager;
    private readonly AppIdentityDbContext _identityContext;
    private readonly HybridCache _hybridCache;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<PermissionService> _logger;

    // In-memory caches for frequently accessed data
    private readonly ConcurrentDictionary<string, string[]> _rolePermissionCache = new();
    private readonly ConcurrentDictionary<long, DateTime> _userCacheTimestamps = new();
    private readonly ConcurrentDictionary<string, bool> _fastPermissionCache = new();

    // Performance tracking
    private long _totalRequests = 0;
    private long _cacheHits = 0;
    private long _cacheMisses = 0;
    private DateTime _lastCacheInvalidation = DateTime.UtcNow;

    // Cache configuration
    private static readonly TimeSpan _hybridCacheExpiration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan _inMemoryCacheExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _fastCacheExpiration = TimeSpan.FromMinutes(1);

    public PermissionService(
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager,
        AppIdentityDbContext identityContext,
        HybridCache hybridCache,
        ITenantContext tenantContext,
        ILogger<PermissionService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _identityContext = identityContext;
        _hybridCache = hybridCache;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IEnumerable<string>>> GetUserPermissionsAsync(long userId, CancellationToken cancellationToken = default)
    {
        try
        {
            Interlocked.Increment(ref _totalRequests);
            var cacheKey = $"user_permissions_{userId}_{_tenantContext.TenantId}";

            var permissions = await _hybridCache.GetOrCreateAsync(
                cacheKey,
                async cancel =>
                {
                    Interlocked.Increment(ref _cacheMisses);
                    var result = await ComputeUserPermissionsAsync(userId);
                    return result.IsSuccess ? result.Value.ToArray() : Array.Empty<string>();
                },
                cancellationToken: cancellationToken);

            if (permissions.Length > 0)
            {
                Interlocked.Increment(ref _cacheHits);
            }

            return Result.Success(permissions.AsEnumerable());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting permissions for user {UserId} in tenant {TenantId}",
                userId, _tenantContext.TenantId);
            return Result.Error("Failed to retrieve user permissions");
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> HasPermissionAsync(long userId, string permission, CancellationToken cancellationToken = default)
    {
        try
        {
            Interlocked.Increment(ref _totalRequests);

            // Fast cache check for frequently accessed permissions
            var fastCacheKey = $"{userId}_{_tenantContext.TenantId}_{permission}";
            if (_fastPermissionCache.TryGetValue(fastCacheKey, out var cachedResult))
            {
                Interlocked.Increment(ref _cacheHits);
                return Result.Success(cachedResult);
            }

            // Quick admin check
            if (await IsUserAdminInternalAsync(userId))
            {
                _fastPermissionCache.TryAdd(fastCacheKey, true);
                _ = Task.Delay(_fastCacheExpiration).ContinueWith(_ =>
                    _fastPermissionCache.TryRemove(fastCacheKey, out var _));

                Interlocked.Increment(ref _cacheHits);
                return Result.Success(true);
            }

            // Get all permissions and check
            var permissionsResult = await GetUserPermissionsAsync(userId, cancellationToken);
            if (!permissionsResult.IsSuccess)
            {
                return Result<bool>.Error(string.Join(", ", permissionsResult.Errors));
            }

            var hasPermission = permissionsResult.Value.Contains(permission);

            // Cache the result for fast access
            _fastPermissionCache.TryAdd(fastCacheKey, hasPermission);
            _ = Task.Delay(_fastCacheExpiration).ContinueWith(_ =>
                _fastPermissionCache.TryRemove(fastCacheKey, out var _));

            return Result.Success(hasPermission);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission {Permission} for user {UserId}", permission, userId);
            return Result.Error("Failed to check user permission");
        }
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, bool>>> HasPermissionsAsync(long userId, IEnumerable<string> permissions, CancellationToken cancellationToken = default)
    {
        try
        {
            var permissionsArray = permissions.ToArray();
            if (permissionsArray.Length == 0)
            {
                return Result.Success(new Dictionary<string, bool>());
            }

            // Quick admin check - if admin, all permissions are true
            if (await IsUserAdminInternalAsync(userId))
            {
                var adminResult = permissionsArray.ToDictionary(p => p, _ => true);
                return Result.Success(adminResult);
            }

            // Get all user permissions once
            var userPermissionsResult = await GetUserPermissionsAsync(userId, cancellationToken);
            if (!userPermissionsResult.IsSuccess)
            {
                return Result<Dictionary<string, bool>>.Error(string.Join(", ", userPermissionsResult.Errors));
            }

            var userPermissionsSet = userPermissionsResult.Value.ToHashSet();
            var result = permissionsArray.ToDictionary(p => p, userPermissionsSet.Contains);

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking multiple permissions for user {UserId}", userId);
            return Result.Error("Failed to check user permissions");
        }
    }

    /// <inheritdoc />
    public async Task<Result<UserRoleInfo>> GetUserRoleInfoAsync(long userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"user_role_info_{userId}_{_tenantContext.TenantId}";

            var roleInfo = await _hybridCache.GetOrCreateAsync(
                cacheKey,
                async cancel => await ComputeUserRoleInfoAsync(userId),
                cancellationToken: cancellationToken);

            return Result.Success(roleInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role info for user {UserId}", userId);
            return Result.Error("Failed to retrieve user role information");
        }
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<long, UserRoleInfo>>> GetUsersRoleInfoAsync(IEnumerable<long> userIds, CancellationToken cancellationToken = default)
    {
        try
        {
            var userIdsArray = userIds.ToArray();
            var tasks = userIdsArray.Select(async userId =>
            {
                var result = await GetUserRoleInfoAsync(userId, cancellationToken);
                return new { UserId = userId, Result = result };
            });

            var results = await Task.WhenAll(tasks);
            var successResults = results
                .Where(r => r.Result.IsSuccess)
                .ToDictionary(r => r.UserId, r => r.Result.Value);

            return Result.Success(successResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role info for multiple users");
            return Result.Error("Failed to retrieve users role information");
        }
    }


    /// <inheritdoc />
    public async Task InvalidateUserPermissionCacheAsync(long userId)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            var userPermissionKey = $"user_permissions_{userId}_{tenantId}";
            var userRoleInfoKey = $"user_role_info_{userId}_{tenantId}";

            await _hybridCache.RemoveAsync(userPermissionKey);
            await _hybridCache.RemoveAsync(userRoleInfoKey);

            // Clear fast cache entries for this user
            var keysToRemove = _fastPermissionCache.Keys
                .Where(k => k.StartsWith($"{userId}_{tenantId}_"))
                .ToArray();

            foreach (var key in keysToRemove)
            {
                _fastPermissionCache.TryRemove(key, out _);
            }

            _userCacheTimestamps.TryRemove(userId, out _);
            _lastCacheInvalidation = DateTime.UtcNow;

            _logger.LogDebug("Invalidated permission cache for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache for user {UserId}", userId);
        }
    }

    /// <inheritdoc />
    public async Task InvalidateRolePermissionCacheAsync(long roleId)
    {
        try
        {
            var role = await _roleManager.FindByIdAsync(roleId.ToString());
            if (role?.Name != null)
            {
                _rolePermissionCache.TryRemove(role.Name, out _);

                // Invalidate all users with this role
                var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);
                var tasks = usersInRole.Select(user => InvalidateUserPermissionCacheAsync(user.Id));
                await Task.WhenAll(tasks);

                _lastCacheInvalidation = DateTime.UtcNow;
                _logger.LogDebug("Invalidated role permission cache for role {RoleId}", roleId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache for role {RoleId}", roleId);
        }
    }

    public Task InvalidateTenantPermissionCacheAsync(long tenantId)
    {
        try
        {
            // This is a heavy operation - should be used sparingly
            var pattern = $"*_{tenantId}_*";

            // Clear in-memory caches
            var keysToRemove = _fastPermissionCache.Keys
                .Where(k => k.Contains($"_{tenantId}_"))
                .ToArray();

            foreach (var key in keysToRemove)
            {
                _fastPermissionCache.TryRemove(key, out _);
            }

            _lastCacheInvalidation = DateTime.UtcNow;
            _logger.LogWarning("Invalidated all permission cache for tenant {TenantId}", tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache for tenant {TenantId}", tenantId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task PreWarmUserPermissionCacheAsync(long userId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Pre-load permissions into cache
            await GetUserPermissionsAsync(userId, cancellationToken);
            await GetUserRoleInfoAsync(userId, cancellationToken);

            _userCacheTimestamps.TryAdd(userId, DateTime.UtcNow);
            _logger.LogDebug("Pre-warmed permission cache for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pre-warming cache for user {UserId}", userId);
        }
    }

    /// <inheritdoc />
    public async Task PreWarmUsersPermissionCacheAsync(IEnumerable<long> userIds, CancellationToken cancellationToken = default)
    {
        try
        {
            var tasks = userIds.Select(userId => PreWarmUserPermissionCacheAsync(userId, cancellationToken));
            await Task.WhenAll(tasks);

            _logger.LogDebug("Pre-warmed permission cache for {UserCount} users", userIds.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pre-warming cache for multiple users");
        }
    }

    /// <summary>
    /// Pre-warms the role permission cache for better performance.
    /// This method is useful for application startup or after role changes.
    /// </summary>
    public async Task PreWarmRolePermissionCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (await HasDatabasePermissionsAsync())
            {
                // Get all roles and warm cache with database permissions
                var roles = await _roleManager.Roles.Where(r => r.IsActive).ToListAsync(cancellationToken);
                var roleIds = roles.Select(r => r.Id).ToArray();

                var permissionsByRole = await GetMultipleRolePermissionsAsync(roleIds);

                // Populate cache
                foreach (var role in roles)
                {
                    if (permissionsByRole.TryGetValue(role.Id, out var permissions))
                    {
                        _rolePermissionCache.TryAdd(role.Name!, permissions);
                    }
                }

                _logger.LogInformation("Pre-warmed role permission cache for {RoleCount} roles from database", roles.Count);
            }
            else
            {
                // Warm cache with code-defined permissions
                foreach (var roleName in Authorization.Roles.AllRoles)
                {
                    var permissions = GetCodeDefinedRolePermissions(roleName);
                    _rolePermissionCache.TryAdd(roleName, permissions);
                }

                _logger.LogInformation("Pre-warmed role permission cache for {RoleCount} roles from code definitions",
                    Authorization.Roles.AllRoles.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pre-warming role permission cache");
        }
    }

    public Task<PermissionCacheStats> GetCacheStatsAsync()
    {
        var stats = new PermissionCacheStats
        {
            TotalRequests = _totalRequests,
            CacheHits = _cacheHits,
            CacheMisses = _cacheMisses,
            ActiveUsers = _userCacheTimestamps.Count,
            CachedRoles = _rolePermissionCache.Count,
            AverageResponseTime = TimeSpan.FromMilliseconds(5), // Would need actual measurement
            LastCacheInvalidation = _lastCacheInvalidation
        };

        return Task.FromResult(stats);
    }

    private async Task<Result<IEnumerable<string>>> ComputeUserPermissionsAsync(long userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result.Error("User not found");
        }

        // Quick admin check
        if (await IsUserAdminInternalAsync(userId))
        {
            return Result.Success(new[] { Actions.Admin.All }.AsEnumerable());
        }

        var userRoles = await _userManager.GetRolesAsync(user);
        var allPermissions = new HashSet<string>();

        foreach (var roleName in userRoles)
        {
            var rolePermissions = await GetRolePermissionsAsync(roleName);
            foreach (var permission in rolePermissions)
            {
                allPermissions.Add(permission);
            }
        }

        return Result.Success(allPermissions.AsEnumerable());
    }

    private async Task<UserRoleInfo> ComputeUserRoleInfoAsync(long userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new UserRoleInfo
            {
                UserId = userId,
                TenantId = _tenantContext.TenantId,
                Roles = Array.Empty<string>(),
                Permissions = Array.Empty<string>(),
                IsAdmin = false,
                CachedAt = DateTime.UtcNow,
                CacheExpiresIn = _hybridCacheExpiration,
                ETag = GenerateETag(userId, Array.Empty<string>(), Array.Empty<string>()),
                FromCache = false
            };
        }

        var isAdmin = await IsUserAdminInternalAsync(userId);
        var userRoles = await _userManager.GetRolesAsync(user);

        var permissions = Array.Empty<string>();
        if (isAdmin)
        {
            permissions = new[] { Actions.Admin.All };
        }
        else
        {
            var permissionSet = new HashSet<string>();
            foreach (var roleName in userRoles)
            {
                var rolePermissions = await GetRolePermissionsAsync(roleName);
                foreach (var permission in rolePermissions)
                {
                    permissionSet.Add(permission);
                }
            }
            permissions = permissionSet.ToArray();
        }

        return new UserRoleInfo
        {
            UserId = userId,
            TenantId = user.TenantId,
            Roles = userRoles.ToArray(),
            Permissions = permissions,
            IsAdmin = isAdmin,
            CachedAt = DateTime.UtcNow,
            CacheExpiresIn = _hybridCacheExpiration,
            ETag = GenerateETag(userId, userRoles.ToArray(), permissions),
            FromCache = false
        };
    }

    private async Task<string[]> GetRolePermissionsAsync(string roleName)
    {
        // Check in-memory cache first
        if (_rolePermissionCache.TryGetValue(roleName, out var cachedPermissions))
        {
            return cachedPermissions;
        }

        // Get role
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role == null)
        {
            return Array.Empty<string>();
        }

        string[] rolePermissions;

        // Check if we have database permissions seeded
        if (await HasDatabasePermissionsAsync())
        {
            // Use optimized database query for performance
            rolePermissions = await GetRolePermissionsFromDatabaseAsync(role.Id);
        }
        else
        {
            // Fall back to code-defined permissions
            rolePermissions = GetCodeDefinedRolePermissions(roleName);
        }

        // Cache for short term
        _rolePermissionCache.TryAdd(roleName, rolePermissions);

        // Remove from cache after expiration
        _ = Task.Delay(_inMemoryCacheExpiration).ContinueWith(_ =>
            _rolePermissionCache.TryRemove(roleName, out var _));

        return rolePermissions;
    }

    private async Task<bool> IsUserAdminInternalAsync(long userId)
    {
        // Use the same cache key pattern as other methods
        var adminCacheKey = $"admin_status:{userId}";

        // Check fast cache first
        if (_fastPermissionCache.TryGetValue(adminCacheKey, out var cachedAdminStatus))
        {
            return (bool)cachedAdminStatus;
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            // Cache negative result
            _fastPermissionCache.TryAdd(adminCacheKey, false);
            _ = Task.Delay(_fastCacheExpiration).ContinueWith(_ =>
                _fastPermissionCache.TryRemove(adminCacheKey, out var _));
            return false;
        }

        var roles = await _userManager.GetRolesAsync(user);
        var isAdmin = roles.Contains(Roles.Admin);

        // Cache the result
        _fastPermissionCache.TryAdd(adminCacheKey, isAdmin);
        _ = Task.Delay(_fastCacheExpiration).ContinueWith(_ =>
            _fastPermissionCache.TryRemove(adminCacheKey, out var _));

        return isAdmin;
    }

    private static string[] GetCodeDefinedRolePermissions(string roleName)
    {
        // Use the code-defined roles and permissions from the Authorization namespace
        return Authorization.Roles.GetAllPermissionsForRole(roleName);
    }

    /// <summary>
    /// Checks if database permissions are seeded and available.
    /// This allows graceful fallback to code-defined permissions during development.
    /// </summary>
    private async Task<bool> HasDatabasePermissionsAsync()
    {
        try
        {
            // Quick check: if we have any permissions in the database, we're using database-driven permissions
            return await _identityContext.Permissions.AnyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check database permissions availability, falling back to code-defined permissions");
            return false;
        }
    }

    /// <summary>
    /// Gets role permissions from database using optimized query with projection.
    /// This method provides the best performance for database-driven permissions.
    /// </summary>
    private async Task<string[]> GetRolePermissionsFromDatabaseAsync(long roleId)
    {
        try
        {
            // Optimized query: Join RolePermissions + Permissions with projection
            // Only selects permission names (not full entities) for better performance
            var permissions = await _identityContext.RolePermissions
                .Where(rp => rp.RoleId == roleId && rp.IsActive && (rp.ExpiresAt == null || rp.ExpiresAt > DateTime.UtcNow))
                .Join(_identityContext.Permissions,
                      rp => rp.PermissionId,
                      p => p.Id,
                      (rp, p) => p.Name)
                .ToArrayAsync();

            _logger.LogDebug("Retrieved {PermissionCount} permissions from database for role {RoleId}",
                permissions.Length, roleId);

            return permissions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions from database for role {RoleId}, falling back to empty array", roleId);
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Alternative method using Include for cases where you need full Permission entities.
    /// Use this when you need more than just permission names.
    /// </summary>
    private async Task<string[]> GetRolePermissionsWithIncludeAsync(long roleId)
    {
        try
        {
            var permissions = await _identityContext.RolePermissions
                .Where(rp => rp.RoleId == roleId && rp.IsActive && (rp.ExpiresAt == null || rp.ExpiresAt > DateTime.UtcNow))
                .Include(rp => rp.Permission)
                .Select(rp => rp.Permission.Name)
                .ToArrayAsync();

            return permissions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions with include for role {RoleId}", roleId);
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Batch method to get permissions for multiple roles efficiently.
    /// Useful for warming cache or bulk operations.
    /// </summary>
    private async Task<Dictionary<long, string[]>> GetMultipleRolePermissionsAsync(IEnumerable<long> roleIds)
    {
        try
        {
            var roleIdArray = roleIds.ToArray();
            if (roleIdArray.Length == 0)
            {
                return new Dictionary<long, string[]>();
            }

            var permissionsByRole = await _identityContext.RolePermissions
                .Where(rp => roleIdArray.Contains(rp.RoleId) && rp.IsActive && (rp.ExpiresAt == null || rp.ExpiresAt > DateTime.UtcNow))
                .Join(_identityContext.Permissions,
                      rp => rp.PermissionId,
                      p => p.Id,
                      (rp, p) => new { rp.RoleId, p.Name })
                .GroupBy(x => x.RoleId)
                .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.Name).ToArray());

            _logger.LogDebug("Retrieved permissions for {RoleCount} roles in batch operation", permissionsByRole.Count);

            return permissionsByRole;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving multiple role permissions");
            return new Dictionary<long, string[]>();
        }
    }

    private static string GenerateETag(long userId, string[] roles, string[] permissions)
    {
        var content = $"{userId}:{string.Join(",", roles)}:{string.Join(",", permissions)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash)[..8]; // Short ETag
    }

    public async Task<Result<bool>> IsUserAdminAsync(long userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var isAdmin = await IsUserAdminInternalAsync(userId);
            return Result.Success(isAdmin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking admin status for user {UserId}", userId);
            return Result.Error("Failed to check admin status");
        }
    }
}
