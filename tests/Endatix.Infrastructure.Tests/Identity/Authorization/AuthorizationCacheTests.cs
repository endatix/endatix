using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Identity.Authorization;
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
    private static readonly Guid _deployment = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid _tenantVersion = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly StubAccessVersionStore _versionStore;
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
        _versionStore = new StubAccessVersionStore(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        _cache = new AuthorizationCache(_hybridCache, _dateTimeProvider, _httpContextAccessor, jwtOptions, _versionStore);
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
    public async Task GetOrCreateAsync_WithValidPrincipal_SetsCacheMetadataForSixtySeconds()
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
        result.ExpiresAt.Should().Be(_now.UtcDateTime.AddSeconds(60));
        result.ETag.Should().NotBeNullOrEmpty();
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Expiration.Should().Be(TimeSpan.FromSeconds(60));
        capturedOptions.LocalCacheExpiration.Should().Be(TimeSpan.FromSeconds(60));
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
        capturedKey.Should().StartWith($"{_deployment:N}:jwt_auth:");
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
        capturedKey.Should().StartWith($"{_deployment:N}:jwt_auth:");
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
        capturedKeys[0].Should().StartWith($"{_deployment:N}:jwt_auth:");
        capturedKeys[0].Should().NotBe(capturedKeys[1]);
        capturedKeys[0].Should().NotContain(jti);
        capturedKeys[0].Should().NotContain(accessToken);
    }

    [Fact]
    public async Task GetOrCreateAsync_ByUserIdWithoutPrincipal_EntryLivesSixtySeconds()
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
        result.ExpiresAt.Should().Be(_now.UtcDateTime.AddSeconds(60));
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Expiration.Should().Be(TimeSpan.FromSeconds(60));
        capturedOptions.LocalCacheExpiration.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task GetOrCreateAsync_AccessVersionChanges_UsesNewKeyForSameToken()
    {
        // Arrange
        var principal = CreatePrincipal("123", "jti-1");
        var capturedKeys = new List<string>();
        _hybridCache.GetOrCreateAsync(
                Arg.Do<string>(key => capturedKeys.Add(key)),
                Arg.Any<Func<CancellationToken, ValueTask<AuthorizationData>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Func<CancellationToken, ValueTask<AuthorizationData>>>()(CancellationToken.None));
        _versionStore.Then(Guid.Parse("22222222-2222-2222-2222-222222222222"));

        // Act
        await _cache.GetOrCreateAsync(principal, _ => Task.FromResult(Result.Success(CreateAuthorizationData("123"))), CancellationToken.None);
        await _cache.GetOrCreateAsync(principal, _ => Task.FromResult(Result.Success(CreateAuthorizationData("123"))), CancellationToken.None);

        // Assert
        capturedKeys.Should().HaveCount(2);
        capturedKeys[0].Should().StartWith($"{_deployment:N}:jwt_auth:11111111111111111111111111111111{_tenantVersion:N}:");
        capturedKeys[1].Should().StartWith($"{_deployment:N}:jwt_auth:22222222222222222222222222222222{_tenantVersion:N}:");
    }

    [Fact]
    public async Task GetOrCreateAsync_ByUserId_KeyCarriesAccessVersion()
    {
        // Arrange
        string? capturedKey = null;
        _hybridCache.GetOrCreateAsync(
                Arg.Do<string>(key => capturedKey = key),
                Arg.Any<Func<CancellationToken, ValueTask<AuthorizationData>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Func<CancellationToken, ValueTask<AuthorizationData>>>()(CancellationToken.None));

        // Act
        await _cache.GetOrCreateAsync("123", 42, null, _ => Task.FromResult(Result.Success(CreateAuthorizationData("123"))), CancellationToken.None);

        // Assert
        capturedKey.Should().Be($"{_deployment:N}:usr_auth:11111111111111111111111111111111{_tenantVersion:N}:123:42");
        _versionStore.ReadTenants.Should().Equal(42);
    }

    [Fact]
    public async Task GetOrCreateAsync_TenantPrincipal_ReadsVersionsForItsTenant()
    {
        // Arrange
        var principal = CreatePrincipal("123", "jti-1", TenantClaim(7));
        CaptureKeysAndRunFactory();

        // Act
        await _cache.GetOrCreateAsync(principal, _ => Task.FromResult(Result.Success(CreateAuthorizationData("123"))), CancellationToken.None);

        // Assert
        _versionStore.ReadTenants.Should().Equal(7);
    }

    [Fact]
    public async Task GetOrCreateAsync_NoTokenAndNoJti_FallbackKeyCarriesTenant()
    {
        // Arrange — the same user in two tenants, with no access token and no jti claim.
        var keys = CaptureKeysAndRunFactory();

        // Act
        await _cache.GetOrCreateAsync(CreatePrincipal("123", null, TenantClaim(1)), _ => Task.FromResult(Result.Success(CreateAuthorizationData("123"))), CancellationToken.None);
        await _cache.GetOrCreateAsync(CreatePrincipal("123", null, TenantClaim(2)), _ => Task.FromResult(Result.Success(CreateAuthorizationData("123"))), CancellationToken.None);

        // Assert
        keys.Should().HaveCount(2);
        keys[0].Should().NotBe(keys[1]);
    }

    [Fact]
    public async Task GetOrCreateAsync_TokenExpiresWithinAMinute_EntryLivesUntilTheTokenBuffer()
    {
        // Arrange — the token expires in 40 s; entries stop 10 s before the token.
        var principal = CreatePrincipal("123", "jti-1", ExpiryClaim(_now.AddSeconds(40)));
        HybridCacheEntryOptions? capturedOptions = null;
        _hybridCache.GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, ValueTask<AuthorizationData>>>(),
                Arg.Do<HybridCacheEntryOptions?>(opt => capturedOptions = opt),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Func<CancellationToken, ValueTask<AuthorizationData>>>()(CancellationToken.None));

        // Act
        var result = await _cache.GetOrCreateAsync(principal, _ => Task.FromResult(Result.Success(CreateAuthorizationData("123"))), CancellationToken.None);

        // Assert
        capturedOptions!.Expiration.Should().Be(TimeSpan.FromSeconds(30));
        result.ExpiresAt.Should().Be(_now.UtcDateTime.AddSeconds(30));
    }

    [Fact]
    public async Task GetOrCreateAsync_TokenLivesLongerThanAMinute_EntryLivesSixtySeconds()
    {
        // Arrange
        var principal = CreatePrincipal("123", "jti-1", ExpiryClaim(_now.AddMinutes(15)));
        HybridCacheEntryOptions? capturedOptions = null;
        _hybridCache.GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, ValueTask<AuthorizationData>>>(),
                Arg.Do<HybridCacheEntryOptions?>(opt => capturedOptions = opt),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Func<CancellationToken, ValueTask<AuthorizationData>>>()(CancellationToken.None));

        // Act
        await _cache.GetOrCreateAsync(principal, _ => Task.FromResult(Result.Success(CreateAuthorizationData("123"))), CancellationToken.None);

        // Assert
        capturedOptions!.Expiration.Should().Be(TimeSpan.FromSeconds(60));
        capturedOptions.LocalCacheExpiration.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task GetOrCreateAsync_PermissionExpiresBeforeTheEntry_ExpiresWithThePermission()
    {
        // Arrange — the data holds a role permission that expires in 20 s.
        var principal = CreatePrincipal("123", "jti-1");
        CaptureKeysAndRunFactory();
        var data = CreateAuthorizationData("123", expiresAt: _now.UtcDateTime.AddSeconds(20));

        // Act
        var result = await _cache.GetOrCreateAsync(principal, _ => Task.FromResult(Result.Success(data)), CancellationToken.None);

        // Assert
        result.ExpiresAt.Should().Be(_now.UtcDateTime.AddSeconds(20));
    }

    [Fact]
    public async Task InvalidateAsync_UserInTenant_RemovesUserAndUserTenantTags()
    {
        // Arrange
        const string userId = "123";
        const long tenantId = 42;

        // Act
        await _cache.InvalidateAsync(userId, tenantId, CancellationToken.None);

        // Assert
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
    private List<string> CaptureKeysAndRunFactory()
    {
        List<string> keys = [];
        _hybridCache.GetOrCreateAsync(
                Arg.Do<string>(keys.Add),
                Arg.Any<Func<CancellationToken, ValueTask<AuthorizationData>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Func<CancellationToken, ValueTask<AuthorizationData>>>()(CancellationToken.None));
        return keys;
    }

    private static ClaimsPrincipal CreatePrincipal(string userId, string? jti = null, params Claim[] extraClaims)
    {
        var claims = new List<Claim>
        {
            new(ClaimNames.UserId, userId)
        };

        if (jti is not null)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
        }

        claims.AddRange(extraClaims);
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static Claim TenantClaim(long tenantId) =>
        new(ClaimNames.TenantId, tenantId.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static Claim ExpiryClaim(DateTimeOffset expiresAt) =>
        new(JwtRegisteredClaimNames.Exp, expiresAt.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static AuthorizationData CreateAuthorizationData(string userId, DateTime expiresAt = default) =>
        AuthorizationData.ForAuthenticatedUser(
            userId: userId,
            tenantId: 42,
            roles: [],
            permissions: [],
            expiresAt: expiresAt);

    #endregion

    /// <summary>Returns its system versions in order and then repeats the last one; records the tenants read.</summary>
    private sealed class StubAccessVersionStore(Guid firstSystemVersion) : IAccessVersionStore
    {
        private readonly Queue<Guid> _next = new();
        private Guid _current = firstSystemVersion;
        private bool _started;

        public List<long> ReadTenants { get; } = [];

        /// <summary>Queues the system version the next read returns, to simulate an access version change.</summary>
        public void Then(Guid value) => _next.Enqueue(value);

        /// <summary>Returns the first value, then each queued value once, then repeats the last.</summary>
        public Task<AccessVersionKey> ReadAsync(long tenantId, CancellationToken cancellationToken)
        {
            if (_started && _next.Count > 0)
            {
                _current = _next.Dequeue();
            }

            _started = true;
            ReadTenants.Add(tenantId);
            return Task.FromResult(new AccessVersionKey(_deployment, _current, _tenantVersion));
        }

        public void Change(IReadOnlyCollection<long> tenantIds) => throw new NotSupportedException();

        public Task ChangeAsync(IReadOnlyCollection<long> tenantIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
