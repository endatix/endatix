using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Microsoft.AspNetCore.Http;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// Provides access to the current user context using ASP.NET Core's IHttpContextAccessor.
/// </summary>
public class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    /// <inheritdoc/>
    public bool IsAnonymous => !IsAuthenticated;

    /// <inheritdoc/>
    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    /// <inheritdoc/>
    public User? GetCurrentUser()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = ExtractUserId(principal);
        if (userId is null)
        {
            return null;
        }

        var tenantId = ExtractTenantId(principal) ?? 0;
        var email = principal.FindFirst(ClaimTypes.Email)?.Value;
        var userName = principal.Identity?.Name;
        var isVerified = principal.FindFirst(ClaimNames.EmailVerified)?.Value == "true";

        return new User(
            id: userId.Value,
            tenantId: tenantId,
            userName: userName ?? email ?? $"user-{userId}",
            email: email ?? string.Empty,
            isVerified: isVerified
        );
    }

    /// <inheritdoc/>
    public string? GetCurrentUserId()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return ExtractUserId(principal)?.ToString();
    }

    private static long? ExtractUserId(ClaimsPrincipal principal)
    {
        // Priority order: sub (JWT standard) -> NameIdentifier (ASP.NET standard) -> custom user_id (legacy)
        var userIdClaim = principal.FindFirst(ClaimNames.UserId) ?? 
                         principal.FindFirst(ClaimTypes.NameIdentifier) ??
                         principal.FindFirst("sub");
        
        return long.TryParse(userIdClaim?.Value, out var userId) ? userId : null;
    }

    private static long? ExtractTenantId(ClaimsPrincipal principal)
    {
        var tenantIdClaim = principal.FindFirst(ClaimNames.TenantId);
        return long.TryParse(tenantIdClaim?.Value, out var tenantId) ? tenantId : null;
    }
}