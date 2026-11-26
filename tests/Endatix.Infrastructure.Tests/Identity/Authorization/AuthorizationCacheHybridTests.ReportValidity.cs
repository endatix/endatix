using Endatix.Core.Abstractions.Authorization;
using Microsoft.Extensions.Time.Testing;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public sealed partial class AuthorizationCacheHybridTests
{
    [Fact]
    public async Task GetOrCreateAsync_SharedSnapshotAtReportDeadline_RechecksOnColdHost()
    {
        // Arrange
        var deadline = _start.AddSeconds(6);
        var source = Substitute.For<IRequiredActionProvider>();
        source.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>())
            .Returns(new RequiredActionReport([], deadline.ToOffset(TimeSpan.FromHours(3))));
        var first = await SnapshotAsync(CreateInstance(true, source));
        var current = new RequiredAction { Key = "renew", ResolvePath = "/profile", Scope = RequiredActionScope.User, Severity = RequiredActionSeverity.Blocking };
        source.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns([current]);
        _clock.Now.Returns(deadline.AddTicks(-1));
        var peer = await SnapshotAsync(CreateInstance(true, source));

        // Act
        _clock.Now.Returns(deadline);
        var refreshed = await SnapshotAsync(CreateInstance(true, source));

        // Assert
        peer.RequiredActions.Should().BeEmpty();
        peer.ETag.Should().Be(first.ETag);
        refreshed.RequiredActions.Should().ContainSingle().Which.Key.Should().Be("renew");
        refreshed.ETag.Should().NotBe(first.ETag);
    }

    [Fact]
    public async Task GetOrCreateAsync_ReportExpiresDuringCleanup_ReturnsUnavailableWithoutRetrying()
    {
        // Arrange
        var deadline = _start.AddSeconds(1);
        var drain = new DrainProbe { ValidUntil = deadline };
        var cache = CreateDrainCache(new FakeTimeProvider(), drain);
        var read = SnapshotAsync(cache);

        // Act
        try
        {
            await drain.InCallback.Task.WaitAsync(TestGuard, TestContext.Current.CancellationToken);
            _clock.Now.Returns(deadline);
        }
        finally
        {
            drain.AllowCallbackToFinish.TrySetResult();
        }
        var result = await AwaitAsync(read);

        // Assert
        result.RequiredActionsUnavailable.Should().BeTrue();
        result.RequiredActions.Should().BeEmpty();
        drain.Calls.Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateAsync_OptionalFallbackReachesReportDeadline_OmitsItDuringBackoff()
    {
        // Arrange
        var deadline = _start.AddSeconds(10);
        var source = Substitute.For<IRequiredActionProvider>();
        source.CacheFor.Returns(TimeSpan.FromSeconds(5));
        var action = new RequiredAction { Key = "profile", ResolvePath = "/profile", Scope = RequiredActionScope.User, Severity = RequiredActionSeverity.Blocking };
        source.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>())
            .Returns(new RequiredActionReport([action], deadline));
        var cache = CreateInstance(true, source);
        await SnapshotAsync(cache);
        source.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<RequiredAction>>(new HttpRequestException()));

        // Act
        _clock.Now.Returns(_start.AddSeconds(5));
        var lastGood = await SnapshotAsync(cache);
        _clock.Now.Returns(deadline);
        var expired = await SnapshotAsync(cache);

        // Assert
        lastGood.RequiredActions.Should().ContainSingle();
        lastGood.ExpiresAt.Should().Be(deadline.UtcDateTime);
        expired.RequiredActions.Should().BeEmpty();
        expired.RequiredActionsUnavailable.Should().BeFalse();
        expired.ETag.Should().NotBe(lastGood.ETag);
    }
}
