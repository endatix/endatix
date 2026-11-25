using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public sealed partial class AuthorizationCacheHybridTests
{
    [Fact]
    public async Task GetOrCreateAsync_ProvidersAwaitEachOther_BothCompleteWithinTheirBudgets()
    {
        // Arrange
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = CreateInstance(false, new RendezvousProvider(first, second), new RendezvousProvider(second, first));

        // Act
        var snapshot = await SnapshotAsync(cache);

        // Assert
        snapshot.RequiredActionsUnavailable.Should().BeFalse();
        first.Task.IsCompletedSuccessfully.Should().BeTrue();
        second.Task.IsCompletedSuccessfully.Should().BeTrue();
    }

    private sealed class RendezvousProvider(TaskCompletionSource ready, TaskCompletionSource other) : IRequiredActionProvider
    {
        public string CacheConfigurationKey => "paired";
        public bool FailClosed => true;

        public async Task<IReadOnlyList<RequiredAction>> GetPendingAsync(RequiredActionSubject subject, CancellationToken cancellationToken)
        {
            ready.SetResult();
            await other.Task.WaitAsync(cancellationToken);
            return [];
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetOrCreateAsync_ExpiredProviderFails_UsesBoundedOptionalFallbackOnly(bool mandatory)
    {
        // Arrange
        var provider = Substitute.For<IRequiredActionProvider>();
        provider.CacheFor.Returns(TimeSpan.FromSeconds(5));
        provider.FailClosed.Returns(mandatory);
        var report = new RequiredAction { Key = "setup", ResolvePath = "/setup", Scope = RequiredActionScope.User, Severity = RequiredActionSeverity.Blocking };
        provider.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns([report]);
        var cache = CreateInstance(false, provider);
        var good = await SnapshotAsync(cache);

        // Act
        provider.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<RequiredAction>>(new HttpRequestException()));
        _clock.Now.Returns(_start.AddSeconds(5));
        var fallback = await SnapshotAsync(cache);

        // Assert
        good.ExpiresAt.Should().Be(_start.AddSeconds(5).UtcDateTime);
        fallback.RequiredActionsUnavailable.Should().Be(mandatory);
        if (mandatory)
        {
            fallback.RequiredActions.Should().BeEmpty();
        }
        else
        {
            fallback.RequiredActions.Should().ContainSingle().Which.Key.Should().Be("setup");
        }
        _clock.Now.Returns(_start.AddMinutes(1));
        (await SnapshotAsync(cache)).RequiredActions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrCreateAsync_OneProviderDoesNotFinish_OtherProviderStartsAndRemainsFreshThroughRetry()
    {
        // Arrange
        var release = new TaskCompletionSource<IReadOnlyList<RequiredAction>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var starting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slow = Substitute.For<IRequiredActionProvider>();
        slow.Timeout.Returns(TimeSpan.FromMilliseconds(300));
        slow.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns(release.Task);
        var fast = Substitute.For<IRequiredActionProvider>();
        fast.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            starting.TrySetResult();
            return Array.Empty<RequiredAction>();
        });
        var cache = CreateInstance(false, slow, fast);

        // Act
        try
        {
            var read = SnapshotAsync(cache);
            await starting.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            (await read.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).RequiredActionsUnavailable.Should().BeFalse();
            await SnapshotAsync(cache);
            _clock.Now.Returns(_start.AddSeconds(15));
            await SnapshotAsync(cache);
        }
        finally
        {
            release.TrySetResult([]);
        }

        // Assert
        await fast.Received(1).GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>());
        await slow.Received(1).GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>());
    }

    private static Task<AuthorizationData> SnapshotAsync(AuthorizationCache cache) =>
        cache.GetOrCreateAsync(CreatePrincipal(), _ => Task.FromResult(Result.Success(CreateData(default))), TestContext.Current.CancellationToken);
}
