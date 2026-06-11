using Endatix.Infrastructure.Identity.Authentication;
using Microsoft.AspNetCore.Http;

namespace Endatix.Infrastructure.Tests.Identity.Authentication;

public sealed class BearerAccessTokenResolverTests
{
    [Theory]
    [InlineData("Bearer token-123", "token-123")]
    [InlineData("Bearer   token-123  ", "token-123")]
    [InlineData("Bearer ", null)]
    [InlineData("Bearer    ", null)]
    [InlineData("Basic token-123", null)]
    public void Resolve_ReturnsOnlyNonEmptyBearerToken(string? authorizationHeader, string? expectedToken)
    {
        HttpContext httpContext = new DefaultHttpContext();
        if (authorizationHeader is not null)
        {
            httpContext.Request.Headers.Authorization = authorizationHeader;
        }

        IHttpContextAccessor httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var token = BearerAccessTokenResolver.Resolve(httpContextAccessor);

        token.Should().Be(expectedToken);
    }
}
