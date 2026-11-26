using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
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
        var identity = JsonSerializer.Serialize(new
        {
            accessToken,
            Jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value,
            Issuer = principal.GetIssuer(),
            Actors = principal.FindAll(ClaimNames.Actor).Select(c => c.Value),
            userId,
            tenantId
        });
        return CacheKeyFingerprint.ComputeHmacSha256Hex(identity, options.Value.SigningKey);
    }
}
