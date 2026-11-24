using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Identity.Authorization;

internal sealed class AuthorizationTokenFingerprint(IHttpContextAccessor context, IOptions<EndatixJwtOptions> options)
{
    internal string For(ClaimsPrincipal principal, string userId, long tenantId)
    {
        var accessToken = BearerAccessTokenResolver.Resolve(context);
        var identity = !string.IsNullOrWhiteSpace(accessToken)
            ? accessToken
            : principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value ?? $"jti_{userId}_{tenantId}";
        return CacheKeyFingerprint.ComputeHmacSha256Hex(identity, options.Value.SigningKey);
    }
}
