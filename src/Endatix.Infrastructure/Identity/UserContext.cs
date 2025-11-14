using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Infrastructure.Identity.Authentication;
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

        var tenantId = principal.GetTenantId();
        var email = principal.FindFirst(ClaimNames.Email)?.Value;
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
        if (principal is null)
        {
            return null;
        }

        return principal.GetUserId();
    }

    private static long? ExtractUserId(ClaimsPrincipal principal)
    {
        var userId = principal.GetUserId();
        if (long.TryParse(userId, out var userIdLong))
        {
            return userIdLong;
        }

        return null;
    }
}