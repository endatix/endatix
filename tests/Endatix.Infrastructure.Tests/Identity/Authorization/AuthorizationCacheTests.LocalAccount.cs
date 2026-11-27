using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public sealed partial class AuthorizationCacheTests
{
    [Theory]
    [InlineData(true, 123L)]
    [InlineData(true, null)]
    [InlineData(false, 123L)]
    [InlineData(false, null)]
    public async Task GetOrCreateAsync_LegacyEntry_RebuildsTheMappingOnce(bool tokenKey, long? verifiedId)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHybridCache();
        await using var provider = services.BuildServiceProvider();
        var hybrid = provider.GetRequiredService<HybridCache>();
        var options = Options.Create(new EndatixJwtOptions { SigningKey = "test-signing-key-32-characters" });
        var cache = new AuthorizationCache(hybrid, _dateTimeProvider, _httpContextAccessor, options, _versionStore);
        var principal = CreatePrincipal("456", "token-id", TenantClaim(42));
        var versions = await _versionStore.ReadAsync(42, TestContext.Current.CancellationToken);
        var fingerprint = CacheKeyFingerprint.ComputeHmacSha256Hex("token-id", options.Value.SigningKey);
        var oldKey = tokenKey ? $"{versions.Prefix}:jwt_auth:{versions.Versions}:{fingerprint}"
            : $"{versions.Prefix}:usr_auth:{versions.Versions}:456:42";
        await hybrid.SetAsync(oldKey, CreateAuthorizationData("456", _now.UtcDateTime.AddMinutes(1)),
            cancellationToken: TestContext.Current.CancellationToken);
        var lookups = 0;

        // Act
        var first = await Read();
        var second = await Read();

        // Assert
        first.LocalUserId.Should().Be(verifiedId);
        second.LocalUserId.Should().Be(verifiedId);
        lookups.Should().Be(1, "a verified absence is cacheable, but a legacy entry cannot establish attribution");

        Task<AuthorizationData> Read() => tokenKey
            ? cache.GetOrCreateAsync(principal, Resolve, TestContext.Current.CancellationToken)
            : cache.GetOrCreateAsync("456", 42, principal, Resolve, TestContext.Current.CancellationToken);
        Task<Result<AuthorizationData>> Resolve(CancellationToken _)
        {
            lookups++;
            return Task.FromResult(Result.Success(CreateAuthorizationData("456") with { LocalUserId = verifiedId }));
        }
    }
}
