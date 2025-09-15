using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Transforms JWT claims by enriching them with user permissions and roles from the database.
/// This enables FastEndpoints' built-in authorization to work with our RBAC system.
/// </summary>
public sealed class JwtClaimsTransformer(
    IPermissionService permissionService,
    HybridCache hybridCache,
    ILogger<JwtClaimsTransformer> logger) : IClaimsTransformation
{
    private const long DEFAULT_TENANT_ID = 1;
    private static readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(15);

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Only process authenticated users with valid issuers
        var issuer = principal.FindFirstValue(JwtRegisteredClaimNames.Iss);
        if (string.IsNullOrEmpty(issuer))
        {
            return principal;
        }

        // Check if claims have already been transformed (idempotent)
        if (principal.HasClaim(c => c.Type == ClaimNames.Permission))
        {
            return principal;
        }

        if (principal.Identity is not ClaimsIdentity primaryIdentity)
        {
            return principal;
        }

        try
        {
            // Get user ID from JWT
            var userId = ExtractUserId(principal);
            if (userId is null)
            {
                return principal;
            }

            // Add tenant ID if missing
            EnsureTenantClaim(primaryIdentity);

            // Enrich with permissions and roles from database
            await EnrichWithUserPermissionsAsync(primaryIdentity, userId.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during claims transformation");
        }

        return principal;
    }

    private static long? ExtractUserId(ClaimsPrincipal principal)
    {
        // Priority order: sub (JWT standard) -> NameIdentifier (ASP.NET standard) -> custom user_id (legacy)
        var userIdClaim = principal.FindFirst(ClaimNames.UserId) ??
                         principal.FindFirst(ClaimTypes.NameIdentifier) ??
                         principal.FindFirst("sub");

        return long.TryParse(userIdClaim?.Value, out var userId) ? userId : null;
    }

    private static void EnsureTenantClaim(ClaimsIdentity identity)
    {
        if (!identity.HasClaim(c => c.Type == ClaimNames.TenantId))
        {
            identity.AddClaim(new Claim(ClaimNames.TenantId, DEFAULT_TENANT_ID.ToString()));
        }
    }

    private async Task EnrichWithUserPermissionsAsync(ClaimsIdentity identity, long userId)
    {
        var cacheKey = $"user_claims:{userId}";

        var claimsData = await hybridCache.GetOrCreateAsync(
            cacheKey,
            async _ => await GetUserClaimsFromDatabase(userId),
            new HybridCacheEntryOptions
            {
                Expiration = _cacheExpiration,
                LocalCacheExpiration = TimeSpan.FromMinutes(5)
            });

        if (claimsData is null)
        {
            return;
        }

        // Add role claims
        foreach (var role in claimsData.Roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        // Add only essential permission claims
        // TODO: We need to review this and see if we can add all permissions 
        var usedPermissions = GetUsedPermissions();
        foreach (var permission in claimsData.Permissions.Where(p => usedPermissions.Contains(p)))
        {
            identity.AddClaim(new Claim(ClaimNames.Permission, permission));
        }

        // Add admin claim for fast admin checks
        if (claimsData.IsAdmin)
        {
            identity.AddClaim(new Claim(ClaimNames.IsAdmin, "true"));
        }
    }

    private async Task<UserClaimsData?> GetUserClaimsFromDatabase(long userId)
    {
        // Check if user is admin first (performance optimization)
        var isAdminResult = await permissionService.IsUserAdminAsync(userId);
        if (!isAdminResult.IsSuccess)
        {
            return null;
        }

        var isAdmin = isAdminResult.Value;

        // Get user roles and permissions
        var userRoleResult = await permissionService.GetUserRoleInfoAsync(userId);
        if (!userRoleResult.IsSuccess)
        {
            return null;
        }

        var roleInfo = userRoleResult.Value;

        return new UserClaimsData
        {
            Roles = roleInfo.Roles.ToList(),
            Permissions = roleInfo.Permissions.ToList(),
            IsAdmin = isAdmin
        };
    }

    /// <summary>
    /// Gets the set of permissions that are actually used by endpoints.
    /// This reduces memory usage by only adding claims that FastEndpoints will check.
    /// </summary>
    private static HashSet<string> GetUsedPermissions()
    {
        return new HashSet<string>
        {
            // Permissions actually used by current endpoints
            Actions.System.HealthCheck,
            Actions.Admin.All,
            Actions.Forms.Create,
            Actions.Forms.Edit,
            Actions.Submissions.Submit,
            // Add more as endpoints are updated from Allow.AllowAll
        };
    }

    private sealed record UserClaimsData
    {
        public List<string> Roles { get; init; } = [];
        public List<string> Permissions { get; init; } = [];
        public bool IsAdmin { get; init; }
    }
}