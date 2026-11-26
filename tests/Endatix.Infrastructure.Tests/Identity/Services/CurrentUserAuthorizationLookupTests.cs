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
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly CurrentUserAuthorizationService _service;

    public CurrentUserAuthorizationLookupTests() =>
        _service = new CurrentUserAuthorizationService(
            _http,
            _cache,
            _strategies,
            Substitute.For<ITenantContext>(),
            Substitute.For<ILogger<CurrentUserAuthorizationService>>(),
            _clock);

    [Fact]
    public async Task GetAuthorizationDataAsync_HydratedReportExpires_RechecksOriginalTokenIdentity()
    {
        // Arrange
        var deadline = DateTimeOffset.UnixEpoch.AddSeconds(5);
        _clock.Now.Returns(deadline.AddTicks(-1));
        var clear = AuthorizationData.ForAuthenticatedUser("mapped", 42, [], [], expiresAt: deadline.UtcDateTime);
        var token = new ClaimsIdentity([new Claim(ClaimNames.UserId, "external")], "test");
        var principal = new ClaimsPrincipal([token, new AuthorizedIdentity(clear)]);
        _http.HttpContext.Returns(new DefaultHttpContext { User = principal });
        var current = clear with { RequiredActionsUnavailable = true };
        var strategy = Substitute.For<IAuthorizationStrategy>();
        strategy.CanHandle(Arg.Is<ClaimsPrincipal>(p => p.GetUserId() == "external")).Returns(true);
        _strategies.Add(strategy);
        var ct = TestContext.Current.CancellationToken;
        _cache.GetOrCreateAsync(Arg.Is<ClaimsPrincipal>(p => p.GetUserId() == "external"),
            Arg.Any<Func<CancellationToken, Task<Result<AuthorizationData>>>>(), ct).Returns(current);
        (await _service.GetAuthorizationDataAsync(ct)).Value.RequiredActionsUnavailable.Should().BeFalse();

        // Act
        _clock.Now.Returns(deadline);
        var result = await _service.GetAuthorizationDataAsync(ct);

        // Assert
        result.Value.RequiredActionsUnavailable.Should().BeTrue();
    }

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
