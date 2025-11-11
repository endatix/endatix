using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Endpoint for getting current user information including roles and permissions.
/// Used by clients (e.g., Next.js Hub) to fetch fresh permission data without re-authentication.
/// </summary>
public class Me(IUserContext userContext, IPermissionService permissionService)
    : EndpointWithoutRequest<Results<Ok<UserInfoResponse>, UnauthorizedHttpResult>>
{
    public override void Configure()
    {
        Get("auth/me");
        Summary(s =>
        {
            s.Summary = "Get current user information";
            s.Description = "Returns current authenticated user information including roles, permissions, and tenant context. " +
                          "Permissions are resolved server-side for freshness and accuracy.";
            s.Responses[200] = "User information retrieved successfully.";
            s.Responses[401] = "Unauthorized - authentication required.";
        });
    }

    public override async Task<Results<Ok<UserInfoResponse>, UnauthorizedHttpResult>> ExecuteAsync(
        CancellationToken cancellationToken)
    {
        var userId = userContext.GetCurrentUserId();
        if (userId == null || !long.TryParse(userId, out var parsedUserId))
        {
            return TypedResults.Unauthorized();
        }

        // Get user roles and permissions from PermissionService
        var roleInfoResult = await permissionService.GetUserPermissionsInfoAsync(parsedUserId, cancellationToken);
        if (!roleInfoResult.IsSuccess)
        {
            return TypedResults.Unauthorized();
        }

        var roleInfo = roleInfoResult.Value;
        var currentUser = userContext.GetCurrentUser();

        var response = new UserInfoResponse
        {
            UserId = parsedUserId,
            Email = currentUser?.Email ?? string.Empty,
            TenantId = roleInfo.TenantId,
            Roles = roleInfo.Roles,
            Permissions = roleInfo.Permissions,
            IsAdmin = roleInfo.IsAdmin,
            EmailVerified = currentUser?.IsVerified ?? false,
            CachedAt = roleInfo.CachedAt,
            CacheExpiresAt = roleInfo.CachedAt.Add(roleInfo.CacheExpiresIn),
            ETag = roleInfo.ETag
        };

        return TypedResults.Ok(response);
    }
}

/// <summary>
/// Response DTO for auth/me endpoint.
/// </summary>
public class UserInfoResponse
{
    public long UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public long TenantId { get; set; }
    public string[] Roles { get; set; } = [];
    public string[] Permissions { get; set; } = [];
    public bool IsAdmin { get; set; }
    public bool EmailVerified { get; set; }
    public DateTime CachedAt { get; set; }
    public DateTime CacheExpiresAt { get; set; }
    public string ETag { get; set; } = string.Empty;
}
