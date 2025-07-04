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

        // Map ClaimsPrincipal to User (customize as needed)
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = principal.FindFirst(ClaimTypes.Email)?.Value;
        var userName = principal.Identity?.Name;
        var isVerified = principal.FindFirst("email_verified")?.Value == "true";
        var tenantIdClaim = principal.FindFirst("tenant_id")?.Value;

        if (!long.TryParse(userIdClaim, out var userId))
        {
            return null;
        }

        long.TryParse(tenantIdClaim, out var tenantId);

        return new User(
            id: userId,
            tenantId: tenantId,
            userName: userName ?? email ?? $"user-{userId}",
            email: email ?? string.Empty,
            isVerified: isVerified
        );
    }

    /// <inheritdoc/>
    public long? GetCurrentUserId()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        var userIdClaim = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (long.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return null;
    }
}