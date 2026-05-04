using System.Security.Claims;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Multitenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Tests.Multitenancy;

/// <summary>
/// Documents that ReBAC / form-access JWTs carry <c>tid</c> on the authenticated principal after JWT bearer validation,
/// which <see cref="TenantMiddleware"/> uses to set <see cref="ITenantContext"/> (no endpoint-side tenant mutation required).
/// </summary>
public sealed class TenantMiddlewareFormAccessJwtTests
{
    [Fact]
    public async Task InvokeAsync_WhenUserIsAuthenticatedWithTidClaim_SetsTenantFromPrincipal()
    {
        var next = Substitute.For<RequestDelegate>();
        var logger = Substitute.For<ILogger<TenantMiddleware>>();
        HttpContext httpContext = new DefaultHttpContext();
        var tenantContext = new TenantContext();
        var middleware = new TenantMiddleware(next, logger);

        var identity = new ClaimsIdentity(
            authenticationType: "Bearer",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimNames.TenantId, "42"));
        httpContext.User = new ClaimsPrincipal(identity);

        await middleware.InvokeAsync(httpContext, tenantContext);

        tenantContext.TenantId.Should().Be(42);
        await next.Received(1).Invoke(httpContext);
    }
}
