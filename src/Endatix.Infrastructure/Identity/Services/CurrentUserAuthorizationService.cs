using Microsoft.Extensions.Logging;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Core.Abstractions.Authorization;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Endatix.Infrastructure.Identity.Authorization.Exceptions;

namespace Endatix.Infrastructure.Identity.Services;

/// <summary>
/// Current user authorization service with caching and optimization.
/// </summary>
internal sealed class CurrentUserAuthorizationService : ICurrentUserAuthorizationService
{

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationCache _authorizationCache;
    private readonly ITenantContext _tenantContext;
    private readonly IEnumerable<IAuthorizationStrategy> _authorizationStrategies;
    private readonly ILogger<CurrentUserAuthorizationService> _logger;

    public CurrentUserAuthorizationService(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationCache authorizationCache,
        IEnumerable<IAuthorizationStrategy> authorizationStrategies,
        ITenantContext tenantContext,
        ILogger<CurrentUserAuthorizationService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationCache = authorizationCache;
        _authorizationStrategies = authorizationStrategies;
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

        var userId = currentPrincipal.GetUserId();
        if (userId is null)
        {
            return Result.Success(AuthorizationData.ForAnonymousUser(_tenantContext.TenantId));
        }

        if (currentPrincipal.IsHydrated())
        {
            try
            {
                return Result.Success(GetAuthorizationDataFromClaims(currentPrincipal));
            }
            catch (InvalidAuthorizedIdentityException ex)
            {
                _logger.LogWarning(ex, "Invalid authorized identity encountered for user {UserId}", userId);
                return Result.Error("Invalid authorized identity encountered");
            }
        }

        var authorizationStrategy = _authorizationStrategies
             .FirstOrDefault(strategy => strategy.CanHandle(currentPrincipal));
        if (authorizationStrategy is null)
        {
            _logger.LogWarning("No authorization strategy found for user {UserId} and issuer {Issuer}",
                userId,
                currentPrincipal.GetIssuer() ?? "unknown");
            return Result.Error("No authorization provider found for the current user");
        }

        try
        {
            var authorizationData = await _authorizationCache.GetOrCreateAsync(
                currentPrincipal,
                async _ => await authorizationStrategy.GetAuthorizationDataAsync(currentPrincipal, cancellationToken),
                cancellationToken);

            return Result.Success(authorizationData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting authorization data for user {UserId}", userId);
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

        await _authorizationCache.InvalidateAsync(userId, tenantId, cancellationToken);
    }


    /// <summary>
    /// Gets the authorization data from the claims principal using the hydrated <see cref="AuthorizedIdentity"/> if present.
    /// </summary>
    /// <param name="principal">The claims principal to get the hydrated authorization data from.</param>
    /// <returns>The authorization data.</returns>
    /// <exception cref="InvalidAuthorizedIdentityException">Thrown when the claims principal is does not contain valid authorization data via the <see cref="AuthorizedIdentity"/>.</exception>
    private AuthorizationData GetAuthorizationDataFromClaims(ClaimsPrincipal principal)
    {
        if (principal?.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            throw new InvalidAuthorizedIdentityException("The claims principal is not authenticated");
        }

        var endatixIdentity = principal.Identities
            .OfType<AuthorizedIdentity>()
            .FirstOrDefault();

        if (endatixIdentity is null || !endatixIdentity.IsHydrated)
        {
            throw new InvalidAuthorizedIdentityException("The claims principal is not hydrated");
        }

        var userId = principal.GetUserId() ?? throw new InvalidAuthorizedIdentityException("User ID is not found");

        var authorizationData = AuthorizationData.ForAuthenticatedUser(
         userId: userId,
         tenantId: endatixIdentity.TenantId,
         roles: endatixIdentity.Roles.ToArray(),
         permissions: endatixIdentity.Permissions.Distinct().ToArray(),
         cachedAt: endatixIdentity.CachedAt,
         expiresAt: endatixIdentity.CacheExpiresIn,
         eTag: endatixIdentity.ETag);

        if (authorizationData.IsAdmin != endatixIdentity.IsAdmin)
        {
            throw new InvalidAuthorizedIdentityException("IsAdmin flag is not consistent with the claims principal");
        }

        return authorizationData;
    }


}
