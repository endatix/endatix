using Endatix.Core.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Infrastructure.Tests.Caching;

public sealed class HybridCacheExtensionTests
{
    private sealed class DummyRef
    {
        public int Id { get; init; }
    }

    private sealed class DummyData
    {
        public int Id { get; init; }
    }

    [Fact]
    public async Task GetOrDefaultAsync_CacheMiss_ReturnsNull_AndDoesNotSet()
    {
        // Arrange
        var cache = Substitute.For<HybridCache>();

        cache
            .GetOrCreateAsync<DummyRef>(
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, ValueTask<DummyRef>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<DummyRef>((DummyRef)null!));

        // Act
        var result = await cache.GetOrDefaultAsync<DummyRef>("missing", TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();

        await cache.DidNotReceive().SetAsync(
            Arg.Any<string>(),
            Arg.Any<DummyRef>(),
            Arg.Any<HybridCacheEntryOptions?>(),
            Arg.Any<IEnumerable<string>?>(),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetOrCreateResultAsync_CacheHit_DoesNotInvokeFactory_AndDoesNotSetAsync()
    {
        // Arrange
        var cache = Substitute.For<HybridCache>();
        var key = "hit";
        var cached = "cached-value";

        cache
            .GetOrCreateAsync<object, string>(
                Arg.Is<string>(k => k == key),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<string>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string>(cached));

        var factoryInvoked = false;

        // Act
        var result = await cache.GetOrCreateResultAsync(
            key,
            _ =>
            {
                factoryInvoked = true;
                return Task.FromResult(Result<string>.Success("new-value"));
            },
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5) },
            tags: new[] { "tag1" },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(cached);
        factoryInvoked.Should().BeFalse();

        await cache.DidNotReceive().SetAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<HybridCacheEntryOptions?>(),
            Arg.Any<IEnumerable<string>?>(),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetOrCreateResultAsync_CacheMiss_FactoryFailure_DoesNotSetAsync()
    {
        // Arrange
        var cache = Substitute.For<HybridCache>();
        var key = "miss-fail";

        cache
            .GetOrCreateAsync<string>(
                Arg.Is<string>(k => k == key),
                Arg.Any<Func<CancellationToken, ValueTask<string>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, ValueTask<string>>>();
                var ct = callInfo.Arg<CancellationToken>();
                return factory(ct);
            });

        // Act
        var result = await cache.GetOrCreateResultAsync(
            key,
            _ => Task.FromResult(Result<string>.Unauthorized("nope")),
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5) },
            tags: new[] { "tag1" },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsUnauthorized().Should().BeTrue();

        await cache.DidNotReceive().SetAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<HybridCacheEntryOptions?>(),
            Arg.Any<IEnumerable<string>?>(),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetOrCreateResultAsync_CacheMiss_FactorySuccess_SetsAsync()
    {
        // Arrange
        var cache = Substitute.For<HybridCache>();
        var key = "miss-success";
        var created = "created";

        cache
            .GetOrCreateAsync<string>(
                Arg.Is<string>(k => k == key),
                Arg.Any<Func<CancellationToken, ValueTask<string>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, ValueTask<string>>>();
                var ct = callInfo.Arg<CancellationToken>();
                return factory(ct);
            });

        // Act
        var result = await cache.GetOrCreateResultAsync(
            key,
            _ => Task.FromResult(Result<string>.Success(created)),
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5) },
            tags: new[] { "tag1" },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(created);
    }

    [Fact]
    public async Task GetOrCreateCachedResultAsync_CacheHit_DoesNotInvokeFactory_AndDoesNotSetAsync()
    {
        // Arrange
        var cache = Substitute.For<HybridCache>();
        var key = "cached";
        var utcNow = new DateTime(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc);
        var ttl = TimeSpan.FromMinutes(7);
        var cachedEnvelope = Cached<DummyData>.Create(new DummyData { Id = 42 }, utcNow, ttl);

        cache
            .GetOrCreateAsync<object, ICachedData<DummyData>>(
                Arg.Is<string>(k => k == key),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<ICachedData<DummyData>>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ICachedData<DummyData>>(cachedEnvelope));

        var factoryInvoked = false;

        // Act
        var result = await cache.GetOrCreateCachedResultAsync(
            key,
            _ =>
            {
                factoryInvoked = true;
                return Task.FromResult(Result<DummyData>.Success(new DummyData { Id = 1 }));
            },
            ttl,
            utcNow,
            tags: new[] { "tag1" },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(cachedEnvelope);
        factoryInvoked.Should().BeFalse();

        await cache.DidNotReceive().SetAsync(
            Arg.Any<string>(),
            Arg.Any<Cached<DummyData>>(),
            Arg.Any<HybridCacheEntryOptions?>(),
            Arg.Any<IEnumerable<string>?>(),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetOrCreateCachedResultAsync_CacheMiss_FactorySuccess_SetsCachedEnvelope()
    {
        // Arrange
        var cache = Substitute.For<HybridCache>();
        var key = "miss-cached-success";
        var utcNow = new DateTime(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc);
        var ttl = TimeSpan.FromMinutes(7);

        cache
            .GetOrCreateAsync<Cached<DummyData>>(
                Arg.Is<string>(k => k == key),
                Arg.Any<Func<CancellationToken, ValueTask<Cached<DummyData>>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, ValueTask<Cached<DummyData>>>>();
                var ct = callInfo.Arg<CancellationToken>();
                return factory(ct);
            });

        // Act
        var result = await cache.GetOrCreateCachedResultAsync(
            key,
            _ => Task.FromResult(Result<DummyData>.Success(new DummyData { Id = 99 })),
            ttl,
            utcNow,
            tags: new[] { "tag1" },
            cancellationToken: CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.Id.Should().Be(99);
        result.Value.CachedAt.Should().Be(utcNow);
        result.Value.ExpiresAt.Should().Be(utcNow.Add(ttl));
    }

    [Fact]
    public async Task TryGetValueAsync_ReturnsExistsTrue_OnCachedValue()
    {
        // Arrange
        var cache = Substitute.For<HybridCache>();
        var key = "exists";

        cache
            .GetOrCreateAsync<object, string>(
                Arg.Is<string>(k => k == key),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<string>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string>("cached"));

        // Act
        var (exists, value) = await cache.TryGetValueAsync<string>(key, TestContext.Current.CancellationToken);

        // Assert
        exists.Should().BeTrue();
        value.Should().Be("cached");
    }

    [Fact]
    public async Task TryGetValueAsync_ReturnsExistsFalse_OnCacheMiss()
    {
        // Arrange
        var cache = Substitute.For<HybridCache>();
        var key = "miss";

        cache
            .GetOrCreateAsync<object, string>(
                Arg.Is<string>(k => k == key),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<string>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<object, CancellationToken, ValueTask<string>>>();
                var ct = callInfo.Arg<CancellationToken>();
                return factory(null!, ct);
            });

        // Act
        var (exists, value) = await cache.TryGetValueAsync<string>(key, TestContext.Current.CancellationToken);

        // Assert
        exists.Should().BeFalse();
        value.Should().BeNull();
    }
}
