using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

/// <summary>
/// <see cref="AuthorizationCache"/> over a real <see cref="HybridCache"/>: what a hit serves, and what a second
/// instance reads back from the shared distributed cache.
/// </summary>
public sealed class AuthorizationCacheHybridTests : IDisposable
{
    private static readonly AccessVersionKey _versions = new(
        Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("33333333-3333-3333-3333-333333333333"));

    private readonly DateTimeOffset _start = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly SharedDistributedCache _sharedL2 = new();
    private readonly List<ServiceProvider> _instances = [];

    public AuthorizationCacheHybridTests()
    {
        _clock.Now.Returns(_start);
    }

    [Fact]
    public async Task GetOrCreateAsync_CachedPermissionHasExpired_RebuildsTheData()
    {
        // Arrange — the first build holds a role permission that expires in 20 s.
        var cache = CreateInstance(withSharedL2: false);
        var principal = CreatePrincipal();
        var builds = 0;
        Task<Result<AuthorizationData>> Build(CancellationToken _)
        {
            builds++;
            var expiresAt = builds == 1 ? _start.UtcDateTime.AddSeconds(20) : default;
            return Task.FromResult(Result.Success(CreateData(expiresAt)));
        }

        await cache.GetOrCreateAsync(principal, Build, CancellationToken.None);
        _clock.Now.Returns(_start.AddSeconds(25));

        // Act
        var result = await cache.GetOrCreateAsync(principal, Build, CancellationToken.None);

        // Assert
        builds.Should().Be(2);
        result.ExpiresAt.Should().BeAfter(_start.UtcDateTime.AddSeconds(25));
    }

    [Fact]
    public async Task GetOrCreateAsync_CachedPermissionStillValid_ServesTheCachedData()
    {
        // Arrange
        var cache = CreateInstance(withSharedL2: false);
        var principal = CreatePrincipal();
        var builds = 0;
        Task<Result<AuthorizationData>> Build(CancellationToken _)
        {
            builds++;
            return Task.FromResult(Result.Success(CreateData(_start.UtcDateTime.AddSeconds(20))));
        }

        await cache.GetOrCreateAsync(principal, Build, CancellationToken.None);
        _clock.Now.Returns(_start.AddSeconds(15));

        // Act
        await cache.GetOrCreateAsync(principal, Build, CancellationToken.None);

        // Assert
        builds.Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateAsync_SecondInstanceSharesTheDistributedCache_ReadsEveryFieldBack()
    {
        // Arrange — one instance builds and stores the entry; the other has an empty L1, so it must deserialize from L2.
        var writer = CreateInstance(true, new ConfiguredProvider("profile.complete"), new ConfiguredProvider("email.verify"));
        var reader = CreateInstance(true, new ConfiguredProvider("email.verify"), new ConfiguredProvider("profile.complete"));
        var principal = CreatePrincipal();
        var written = await writer.GetOrCreateAsync(
            principal,
            _ => Task.FromResult(Result.Success(CreateData(_start.UtcDateTime.AddSeconds(45)))),
            CancellationToken.None);

        // Act
        var read = await reader.GetOrCreateAsync(
            principal,
            _ => throw new InvalidOperationException("the second instance must read the stored entry"),
            CancellationToken.None);

        // Assert
        read.RequiredActions.Select(a => a.Key).Should().Equal("email.verify", "profile.complete");
        read.Should().BeEquivalentTo(written);
        read.Should().NotBeSameAs(written);
    }

    [Theory]
    [InlineData(null, "v1", false)]
    [InlineData("v1", null, false)]
    [InlineData("v1", "v2", false)]
    [InlineData("v2", "v1", false)]
    [InlineData(null, "v1", true)]
    [InlineData("v1", null, true)]
    [InlineData("v1", "v2", true)]
    [InlineData("v2", "v1", true)]
    public async Task GetOrCreateAsync_DifferentProviderConfigurations_ServeTheirOwnSnapshot(
        string? firstRevision, string? secondRevision, bool byUser)
    {
        var ct = TestContext.Current.CancellationToken;
        var principal = CreatePrincipal();
        var expiresAt = _start.UtcDateTime.AddSeconds(45);
        var authorization = CreateData(expiresAt);
        Task<Result<AuthorizationData>> Build(CancellationToken _) => Task.FromResult(Result.Success(authorization));
        Task<AuthorizationData> ReadAsync(string? revision)
        {
            var cache = CreateInstance(true, revision is null ? [] : [new ConfiguredProvider(revision)]);
            return byUser ? cache.GetOrCreateAsync("123", 42, principal, Build, ct) : cache.GetOrCreateAsync(principal, Build, ct);
        }

        await ReadAsync(firstRevision);
        var read = await ReadAsync(secondRevision);

        read.RequiredActions.Select(a => a.Key).Should().Equal(secondRevision is null ? Array.Empty<string>() : [secondRevision]);
        read.Permissions.Should().BeEquivalentTo(authorization.Permissions);
        read.Roles.Should().BeEquivalentTo(authorization.Roles);
        read.ExpiresAt.Should().Be(expiresAt);
    }

    private sealed class ConfiguredProvider(string revision) : IRequiredActionProvider
    {
        public string CacheConfigurationKey => revision;

        public Task<IReadOnlyList<RequiredAction>> GetPendingAsync(RequiredActionSubject subject, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RequiredAction>>([new()
            {
                Key = revision, ResolvePath = "/profile",
                Scope = RequiredActionScope.User, Severity = RequiredActionSeverity.Notice
            }]);
    }

    public void Dispose()
    {
        foreach (var instance in _instances)
        {
            instance.Dispose();
        }
    }

    [Theory]
    [InlineData(true, false, RequiredActionSeverity.Blocking)]
    [InlineData(false, true, RequiredActionSeverity.Blocking)]
    [InlineData(false, false, RequiredActionSeverity.Notice)]
    public async Task GetOrCreateAsync_AtBlockingFrom_ServesEffectiveSeverityForTheSamePrincipal(
        bool canResolve, bool blockNonResolvers, RequiredActionSeverity expectedSeverity)
    {
        // Arrange
        var deadline = _start.AddSeconds(8);
        var provider = NoticeProvider(canResolve ? "Creator" : "Admin", blockNonResolvers);
        var cache = CreateInstance(false, provider);
        var principal = CreatePrincipal();
        Task<AuthorizationData> ReadAsync() => cache.GetOrCreateAsync(principal,
            _ => Task.FromResult(Result.Success(CreateData(default))), TestContext.Current.CancellationToken);

        // Act
        var before = await ReadAsync();
        _clock.Now.Returns(deadline);
        var after = await ReadAsync();

        // Assert
        before.RequiredActions.Single().Severity.Should().Be(RequiredActionSeverity.Notice);
        after.RequiredActions.Single().Severity.Should().Be(expectedSeverity);
        after.RequiredActions.Single().CanResolve.Should().Be(canResolve);
        if (expectedSeverity == RequiredActionSeverity.Blocking)
        {
            after.ETag.Should().NotBe(before.ETag);
            before.ExpiresAt.Should().Be(deadline.UtcDateTime);
        }
    }

    [Theory]
    [InlineData(18, 18)]
    [InlineData(35, 20)]
    public async Task GetOrCreateAsync_DeadlinePassesDuringRebuild_ReturnsBlockingWithinOriginalExpiry(
        int permissionExpirySeconds, int expectedExpirySeconds)
    {
        var slowProvider = Substitute.For<IRequiredActionProvider>();
        var cache = CreateInstance(false, NoticeProvider("Creator", false), slowProvider);
        var principal = CreatePrincipal();
        principal.Identities.Single().AddClaim(new Claim(JwtRegisteredClaimNames.Exp,
            _start.AddSeconds(30).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Task<AuthorizationData> ReadAsync(int expiry) => cache.GetOrCreateAsync(principal,
            _ => Task.FromResult(Result.Success(CreateData(_start.AddSeconds(expiry).UtcDateTime))),
            TestContext.Current.CancellationToken);
        slowProvider.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<RequiredAction>());
        await ReadAsync(4);
        _clock.Now.Returns(_start.AddSeconds(5));
        slowProvider.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            _clock.Now.Returns(_start.AddSeconds(9));
            return Array.Empty<RequiredAction>();
        });

        var rebuilt = await ReadAsync(permissionExpirySeconds);

        rebuilt.RequiredActions.Single().Severity.Should().Be(RequiredActionSeverity.Blocking);
        rebuilt.ExpiresAt.Should().Be(_start.AddSeconds(expectedExpirySeconds).UtcDateTime);
    }

    private IRequiredActionProvider NoticeProvider(string resolverRole, bool blockNonResolvers)
    {
        RequiredAction action = new()
        {
            Key = "profile.complete",
            ResolvePath = "/profile",
            Scope = RequiredActionScope.Tenant,
            Severity = RequiredActionSeverity.Notice,
            BlockingFrom = _start.AddSeconds(8),
            ResolverRoles = [resolverRole],
            BlockNonResolvers = blockNonResolvers
        };
        var provider = Substitute.For<IRequiredActionProvider>();
        provider.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns([action]);
        return provider;
    }

    private AuthorizationCache CreateInstance(bool withSharedL2, params IRequiredActionProvider[] actionProviders)
    {
        ServiceCollection services = new();
        if (withSharedL2)
        {
            services.AddSingleton<IDistributedCache>(_sharedL2);
        }

        services.AddHybridCache();
        var provider = services.BuildServiceProvider();
        _instances.Add(provider);

        var jwtOptions = Options.Create(new EndatixJwtOptions { SigningKey = "test-signing-key-32-characters" });
        return new AuthorizationCache(
            provider.GetRequiredService<HybridCache>(),
            _clock,
            new(new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, jwtOptions),
            new FixedAccessVersionStore(),
            actionProviders);
    }

    private static ClaimsPrincipal CreatePrincipal() =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimNames.UserId, "123"),
            new Claim(ClaimNames.TenantId, "42"),
            new Claim(JwtRegisteredClaimNames.Jti, "jti-1"),
        ], "test"));

    private static AuthorizationData CreateData(DateTime expiresAt) =>
        AuthorizationData.ForAuthenticatedUser(
            userId: "123",
            tenantId: 42,
            roles: ["Creator"],
            permissions: ["forms.create", "forms.view"],
            expiresAt: expiresAt);

    /// <summary>
    /// A distributed cache two instances share. HybridCache ignores a plain <see cref="MemoryDistributedCache"/> as its
    /// L2, so it is wrapped; the values still go through HybridCache's serializer.
    /// </summary>
    private sealed class SharedDistributedCache : IDistributedCache
    {
        private readonly MemoryDistributedCache _inner = new(Options.Create(new MemoryDistributedCacheOptions()));

        public byte[]? Get(string key) => _inner.Get(key);

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => _inner.GetAsync(key, token);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _inner.Set(key, value, options);

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            _inner.SetAsync(key, value, options, token);

        public void Refresh(string key) => _inner.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) => _inner.RefreshAsync(key, token);

        public void Remove(string key) => _inner.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default) => _inner.RemoveAsync(key, token);
    }

    private sealed class FixedAccessVersionStore : IAccessVersionStore
    {
        public Task<AccessVersionKey> ReadAsync(long tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(_versions);

        public void Change(IReadOnlyCollection<long> tenantIds) => throw new NotSupportedException();

        public Task ChangeAsync(IReadOnlyCollection<long> tenantIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
