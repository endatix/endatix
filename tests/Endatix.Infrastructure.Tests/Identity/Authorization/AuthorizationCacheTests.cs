using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public sealed class AuthorizationCacheTests
{
    private readonly HybridCache _hybridCache;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly HttpContextAccessor _httpContextAccessor;
    private readonly AuthorizationCache _cache;
    private readonly DateTimeOffset _now;

    public AuthorizationCacheTests()
    {
        _hybridCache = Substitute.For<HybridCache>();
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        _now = new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero);
        _dateTimeProvider.Now.Returns(_now);
        var jwtOptions = Options.Create(new EndatixJwtOptions
        {
            SigningKey = "test-signing-key-32-characters",
        });
        _cache = new AuthorizationCache(_hybridCache, _dateTimeProvider, _httpContextAccessor, jwtOptions);
    }

    [Fact]
    public async Task GetOrCreateAsync_NoUserId_ThrowsInvalidOperationException()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var action = async () => await _cache.GetOrCreateAsync(
            principal,
            _ => Task.FromResult(Result.Success(CreateAuthorizationData("any"))),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Principal must have a user ID");
    }

    [Fact]
    public async Task GetOrCreateAsync_WithValidPrincipal_SetsCacheMetadata()
    {
        var principal = CreatePrincipal("123");
        HybridCacheEntryOptions? capturedOptions = null;

        _hybridCache.GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, ValueTask<AuthorizationData>>>(),
                Arg.Do<HybridCacheEntryOptions?>(opt => capturedOptions = opt),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, ValueTask<AuthorizationData>>>();
                var token = callInfo.Arg<CancellationToken>();
                return factory(token);
            });

        var baseData = CreateAuthorizationData("123");

        var result = await _cache.GetOrCreateAsync(
            principal,
            _ => Task.FromResult(Result.Success(baseData)),
            CancellationToken.None);

        result.CachedAt.Should().Be(_now.UtcDateTime);
        result.ExpiresAt.Should().Be(_now.UtcDateTime.AddMinutes(15));
        result.ETag.Should().NotBeNullOrEmpty();
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Expiration.Should().Be(TimeSpan.FromMinutes(15));
        capturedOptions.LocalCacheExpiration.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task GetOrCreateAsync_WithDataFactoryError_ThrowsInvalidOperationException()
    {
        var principal = CreatePrincipal("123");

        _hybridCache.GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, ValueTask<AuthorizationData>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, ValueTask<AuthorizationData>>>();
                var token = callInfo.Arg<CancellationToken>();
                return factory(token);
            });

        var action = async () => await _cache.GetOrCreateAsync(
            principal,
            _ => Task.FromResult(Result<AuthorizationData>.Error("identity store failure")),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("identity store failure");
    }

    [Fact]
    public async Task GetOrCreateAsync_UsesJtiWhenPresent()
    {
        const string jti = "custom-jti";
        var principal = CreatePrincipal("123", jti);
        string? capturedKey = null;

        _hybridCache.GetOrCreateAsync(
                Arg.Do<string>(key => capturedKey = key),
                Arg.Any<Func<CancellationToken, ValueTask<AuthorizationData>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, ValueTask<AuthorizationData>>>();
                var token = callInfo.Arg<CancellationToken>();
                return factory(token);
            });

        await _cache.GetOrCreateAsync(
            principal,
            _ => Task.FromResult(Result.Success(CreateAuthorizationData("123"))),
            CancellationToken.None);

        capturedKey.Should().NotBeNull();
        capturedKey.Should().StartWith("jwt_auth:");
        capturedKey.Should().NotContain(jti);
    }

    [Fact]
    public async Task GetOrCreateAsync_UsesUserIdWhenJtiMissing()
    {
        var principal = CreatePrincipal("123");
        string? capturedKey = null;

        _hybridCache.GetOrCreateAsync(
                Arg.Do<string>(key => capturedKey = key),
                Arg.Any<Func<CancellationToken, ValueTask<AuthorizationData>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, ValueTask<AuthorizationData>>>();
                var token = callInfo.Arg<CancellationToken>();
                return factory(token);
            });

        await _cache.GetOrCreateAsync(
            principal,
            _ => Task.FromResult(Result.Success(CreateAuthorizationData("123"))),
            CancellationToken.None);

        capturedKey.Should().NotBeNull();
        capturedKey.Should().StartWith("jwt_auth:");
        capturedKey.Should().NotContain("jti_123");
    }

    [Fact]
    public async Task GetOrCreateAsync_UsesAccessTokenWhenPresent()
    {
        const string jti = "custom-jti";
        const string accessToken = "raw-access-token";
        var principal = CreatePrincipal("123", jti);
        var capturedKeys = new List<string>();

        _hybridCache.GetOrCreateAsync(
                Arg.Do<string>(key => capturedKeys.Add(key)),
                Arg.Any<Func<CancellationToken, ValueTask<AuthorizationData>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, ValueTask<AuthorizationData>>>();
                var token = callInfo.Arg<CancellationToken>();
                return factory(token);
            });

        _httpContextAccessor.HttpContext!.Request.Headers.Authorization = $"Bearer {accessToken}";

        await _cache.GetOrCreateAsync(
            principal,
            _ => Task.FromResult(Result.Success(CreateAuthorizationData("123"))),
            CancellationToken.None);

        _httpContextAccessor.HttpContext.Request.Headers.Remove("Authorization");

        await _cache.GetOrCreateAsync(
            principal,
            _ => Task.FromResult(Result.Success(CreateAuthorizationData("123"))),
            CancellationToken.None);

        capturedKeys.Should().HaveCount(2);
        capturedKeys[0].Should().StartWith("jwt_auth:");
        capturedKeys[0].Should().NotBe(capturedKeys[1]);
        capturedKeys[0].Should().NotContain(jti);
        capturedKeys[0].Should().NotContain(accessToken);
    }

    [Fact]
    public async Task GetOrCreateAsync_ByUserId_UsesDefaultExpirationWhenPrincipalMissing()
    {
        HybridCacheEntryOptions? capturedOptions = null;

        _hybridCache.GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, ValueTask<AuthorizationData>>>(),
                Arg.Do<HybridCacheEntryOptions?>(opt => capturedOptions = opt),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, ValueTask<AuthorizationData>>>();
                var token = callInfo.Arg<CancellationToken>();
                return factory(token);
            });

        var result = await _cache.GetOrCreateAsync(
            "123",
            42,
            null,
            _ => Task.FromResult(Result.Success(CreateAuthorizationData("123"))),
            CancellationToken.None);

        result.CachedAt.Should().Be(_now.UtcDateTime);
        result.ExpiresAt.Should().Be(_now.UtcDateTime.AddMinutes(15));
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Expiration.Should().Be(TimeSpan.FromMinutes(15));
        capturedOptions.LocalCacheExpiration.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task InvalidateAsync_RemovesUserCacheEntry()
    {
        await _cache.InvalidateAsync("123", 42, CancellationToken.None);

        await _hybridCache.Received(1).RemoveAsync("usr_auth:123:42", Arg.Any<CancellationToken>());
        await _hybridCache.Received(1).RemoveByTagAsync("auth_data:user:123", Arg.Any<CancellationToken>());
        await _hybridCache.Received(1).RemoveByTagAsync("auth_data:user:123:tenant:42", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAllAsync_RemovesAllTags()
    {
        await _cache.InvalidateAllAsync(CancellationToken.None);

        await _hybridCache.Received(1).RemoveByTagAsync(
            Arg.Is<string[]>(tags => tags.Single() == "auth_data:all"),
            Arg.Any<CancellationToken>());
    }

    #region Helper Methods
    private static ClaimsPrincipal CreatePrincipal(string userId, string? jti = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimNames.UserId, userId)
        };

        if (jti is not null)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static AuthorizationData CreateAuthorizationData(string userId) =>
        AuthorizationData.ForAuthenticatedUser(
            userId: userId,
            tenantId: 42,
            roles: [],
            permissions: [],
            cachedAt: DateTime.UtcNow,
            expiresAt: DateTime.UtcNow,
            eTag: string.Empty);

    #endregion
}
