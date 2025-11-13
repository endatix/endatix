using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Core.Abstractions.Authorization;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Endatix.Infrastructure.Identity.Services;

/// <summary>
/// Current user authorization service with caching and optimization.
/// </summary>
internal sealed class CurrentUserAuthorizationService : ICurrentUserAuthorizationService
{

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HybridCache _hybridCache;
    private readonly ITenantContext _tenantContext;
    private readonly IEnumerable<IAuthorizationProvider> _authorizationProviders;
    private readonly ILogger<CurrentUserAuthorizationService> _logger;

    private static readonly TimeSpan _hybridCacheExpiration = TimeSpan.FromMinutes(15);

    public CurrentUserAuthorizationService(
        IHttpContextAccessor httpContextAccessor,
        HybridCache hybridCache,
        IEnumerable<IAuthorizationProvider> authorizationProviders,
        ITenantContext tenantContext,
        ILogger<CurrentUserAuthorizationService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _hybridCache = hybridCache;
        _authorizationProviders = authorizationProviders;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<AuthorizationData>> GetAuthorizationDataAsync(CancellationToken cancellationToken)
    {
        var currentPrincipal = _httpContextAccessor.HttpContext?.User;
        if (currentPrincipal is null)
        {
            return Result.Error("No current user found");
        }

        var authorizationDataResult = ExtractAuthorizationDataFromClaims(currentPrincipal);
        if (authorizationDataResult.IsSuccess)
        {
            return authorizationDataResult;
        }

        var userId = currentPrincipal.GetUserId();
        if (userId is null)
        {
            return Result.Success(AuthorizationData.ForAnonymousUser(_tenantContext.TenantId));
        }

        try
        {
            var cacheKey = GetAuthorizationDataCacheKey(userId, _tenantContext.TenantId);
            var authorizationProvider = _authorizationProviders.FirstOrDefault(provider => provider.CanHandle(currentPrincipal));
            if (authorizationProvider is null)
            {
                return Result.Error("No authorization provider found for the current user");
            }

            var permissionsInfo = await _hybridCache.GetOrCreateAsync(
                cacheKey,
                async cancel => await authorizationProvider.GetAuthorizationDataAsync(currentPrincipal, cancellationToken),
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
    public async Task<Result<bool>> HasPermissionAsync(string permission, CancellationToken cancellationToken)
    {
        var permissionsInfoResult = await GetAuthorizationDataAsync(cancellationToken);
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
    public async Task<Result<Dictionary<string, bool>>> HasPermissionsAsync(IEnumerable<string> permissions, CancellationToken cancellationToken)
    {
        var permissionsInfoResult = await GetAuthorizationDataAsync(cancellationToken);
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
    public async Task<Result<bool>> IsAdminAsync(CancellationToken cancellationToken)
    {
        var permissionsInfoResult = await GetAuthorizationDataAsync(cancellationToken);
        if (!permissionsInfoResult.IsSuccess)
        {
            return Result.Error("Failed to get user permissions info");
        }

        var permissionInfo = permissionsInfoResult.Value;

        return Result.Success(permissionInfo.IsAdmin);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> IsPlatformAdminAsync(CancellationToken cancellationToken)
    {
        var permissionsInfoResult = await GetAuthorizationDataAsync(cancellationToken);
        if (!permissionsInfoResult.IsSuccess)
        {
            return Result.Error("Failed to get user permissions info");
        }

        var permissionInfo = permissionsInfoResult.Value;

        return Result.Success(permissionInfo.Roles.Contains(SystemRole.PlatformAdmin.Name));
    }


    /// <inheritdoc />
    public async Task<Result> ValidateAccessAsync(string requiredPermission, CancellationToken cancellationToken)
    {
        var isAdminResult = await IsAdminAsync(cancellationToken);
        if (isAdminResult.IsSuccess && isAdminResult.Value)
        {
            return Result.Success();
        }

        var hasPermissionResult = await HasPermissionAsync(requiredPermission, cancellationToken);
        if (hasPermissionResult.IsSuccess && hasPermissionResult.Value)
        {
            return Result.Success();
        }

        return Result.Forbidden($"Permission '{requiredPermission}' required to access this resource.");
    }


    /// <inheritdoc />
    public async Task InvalidateAuthorizationDataCacheAsync(CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var userId = _httpContextAccessor.HttpContext?.User.GetUserId();
        if (userId is null)
        {
            return;
        }

        var authorizationDataKey = GetAuthorizationDataCacheKey(userId, tenantId);

        await _hybridCache.RemoveAsync(authorizationDataKey);
    }


    /// <summary>
    /// Extracts the authorization data from the claims principal.
    /// </summary>
    /// <param name="principal">The claims principal to get the hydrated authorization data from.</param>
    /// <returns>The hydrated authorization data if successful, otherwise a not found result.</returns>
    private Result<AuthorizationData> ExtractAuthorizationDataFromClaims(ClaimsPrincipal principal)
    {
        if (!principal.IsHydrated())
        {
            return Result.NotFound("Claims principal is not hydrated");
        }

        var userId = principal.GetUserId();
        if (userId is null)
        {
            return Result.Error("User ID is not found");
        }

        var tenantId = principal.GetTenantId();
        if (tenantId is null || !long.TryParse(tenantId, out var parsedTenantId))
        {
            return Result.Error("Tenant ID is not found");
        }

        var authorizationData = AuthorizationData.ForAuthenticatedUser(
         userId: userId,
         tenantId: parsedTenantId,
         roles: principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray(),
         permissions: principal.FindAll(ClaimNames.Permission).Select(c => c.Value).Distinct().ToArray(),
         cachedAt: DateTime.UtcNow,
         cacheExpiresIn: TimeSpan.Zero,
         eTag: string.Empty);

        return Result.Success(authorizationData);
    }
    private static string GetAuthorizationDataCacheKey(string userId, long tenantId) => $"usr_rbac:{userId}:{tenantId}";
}
