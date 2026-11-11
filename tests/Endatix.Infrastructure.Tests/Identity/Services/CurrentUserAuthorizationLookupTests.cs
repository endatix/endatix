using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Infrastructure.Identity.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Tests.Identity.Services;

public sealed class CurrentUserAuthorizationLookupTests
{
    private readonly IHttpContextAccessor _http = Substitute.For<IHttpContextAccessor>();
    private readonly IAuthorizationCache _cache = Substitute.For<IAuthorizationCache>();
    private readonly List<IAuthorizationStrategy> _strategies = [];
    private readonly CurrentUserAuthorizationService _service;

    public CurrentUserAuthorizationLookupTests() =>
        _service = new CurrentUserAuthorizationService(
            _http,
            _cache,
            _strategies,
            Substitute.For<ITenantContext>(),
            Substitute.For<ILogger<CurrentUserAuthorizationService>>());

    [Fact]
    public async Task PermissionHelpers_WhenLookupFails_ReturnError()
    {
        ArrangeFailedLookup();

        var permission = await _service.HasPermissionAsync("read:forms", CancellationToken.None);
        var permissions = await _service.HasPermissionsAsync(["read:forms"], CancellationToken.None);
        var admin = await _service.IsAdminAsync(CancellationToken.None);
        var platform = await _service.IsPlatformAdminAsync(CancellationToken.None);

        permission.Errors.First().Should().Be("Failed to get user permissions info");
        permissions.Errors.First().Should().Be("Failed to get user permissions info");
        admin.Errors.First().Should().Be("Failed to get user permissions info");
        platform.Errors.First().Should().Be("Failed to get user permissions info");
    }

    private void ArrangeFailedLookup()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimNames.UserId, "123")], "test"));
        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(principal);
        _http.HttpContext.Returns(httpContext);
        var strategy = Substitute.For<IAuthorizationStrategy>();
        strategy.CanHandle(principal).Returns(true);
        strategy.GetAuthorizationDataAsync(principal, Arg.Any<CancellationToken>())
            .Returns(Result<AuthorizationData>.Error("identity store failure"));
        _strategies.Add(strategy);
        _cache.GetOrCreateAsync(
                principal,
                Arg.Any<Func<CancellationToken, Task<Result<AuthorizationData>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var result = await call.Arg<Func<CancellationToken, Task<Result<AuthorizationData>>>>()(CancellationToken.None);
                throw new InvalidOperationException($"Authorization data factory returned status {result.Status}");
            });
    }
}
