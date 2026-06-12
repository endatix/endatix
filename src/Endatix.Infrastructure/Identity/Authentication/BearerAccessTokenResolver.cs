using Microsoft.AspNetCore.Http;

namespace Endatix.Infrastructure.Identity.Authentication;

internal static class BearerAccessTokenResolver
{
    private const string BEARER_PREFIX = "Bearer ";
    /// <summary>
    /// Resolves the bearer access token from the HTTP context accessor.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <returns>The bearer access token or <c>null</c> if not found.</returns>
    public static string? Resolve(IHttpContextAccessor httpContextAccessor)
    {
        var request = httpContextAccessor.HttpContext?.Request;
        var authHeader = request?.Headers.Authorization.FirstOrDefault();
        if (authHeader is null || !authHeader.StartsWith(BEARER_PREFIX, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authHeader[BEARER_PREFIX.Length..].Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }
}
