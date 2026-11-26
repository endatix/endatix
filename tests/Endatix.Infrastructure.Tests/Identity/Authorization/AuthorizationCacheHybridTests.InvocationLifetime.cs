using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public sealed partial class AuthorizationCacheHybridTests
{
    [Fact]
    public async Task GetOrCreateAsync_SuccessEndsInvocationToken_DrainsBeforeDisposalAndKeepsReport()
    {
        // Arrange
        var drain = new DrainProbe();
        var cache = CreateDrainCache(new FakeTimeProvider(), drain);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var read = cache.GetOrCreateAsync(CreatePrincipal(), _ => Task.FromResult(Result.Success(CreateData(default))), caller.Token);

        // Act
        try
        {
            await drain.InCallback.Task.WaitAsync(TestGuard, caller.Token);
            drain.DisposalStarted.Task.IsCompleted.Should().BeFalse();
        }
        finally
        {
            drain.AllowCallbackToFinish.TrySetResult();
        }
        var report = await AwaitAsync(read);

        // Assert
        caller.IsCancellationRequested.Should().BeFalse();
        drain.Drained.Task.IsCompletedSuccessfully.Should().BeTrue();
        report.RequiredActionsUnavailable.Should().BeFalse();
        report.RequiredActions.Should().ContainSingle().Which.Key.Should().Be("first");
        _versionStore.TenantVersions[42] = Guid.NewGuid();
        (await AwaitAsync(SnapshotAsync(cache))).RequiredActions.Should().ContainSingle().Which.Key.Should().Be("next");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetOrCreateAsync_CallerStopsWhileCompletionCallbacksRun_NeverPublishesLateReport(bool cancel)
    {
        // Arrange
        var timer = new FakeTimeProvider();
        var drain = new DrainProbe();
        var cache = CreateDrainCache(timer, drain);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var read = cache.GetOrCreateAsync(CreatePrincipal(), _ => Task.FromResult(Result.Success(CreateData(default))), caller.Token);
        try
        {
            await drain.InCallback.Task.WaitAsync(TestGuard, caller.Token);
            if (cancel)
            {
                await CancelReadAsync(caller, read);
            }
            else
            {
                timer.Advance(HeldProvider.Budget);
                (await AwaitAsync(read)).RequiredActionsUnavailable.Should().BeTrue();
                caller.IsCancellationRequested.Should().BeFalse();
            }
            _versionStore.SystemVersion = Guid.NewGuid();
            (await AwaitAsync(SnapshotAsync(cache))).RequiredActionsUnavailable.Should().BeTrue();
            drain.DisposalStarted.Task.IsCompleted.Should().BeFalse();
        }
        finally
        {
            drain.AllowCallbackToFinish.TrySetResult();
        }

        // Assert
        await drain.Drained.Task.WaitAsync(TestGuard, TestContext.Current.CancellationToken);
        var after = await AwaitAsync(SnapshotAsync(cache));
        after.RequiredActionsUnavailable.Should().BeTrue();
        after.RequiredActions.Should().BeEmpty();
    }

    private AuthorizationCache CreateDrainCache(TimeProvider timer, DrainProbe drain) =>
        CreateInstance(false, services =>
        {
            services.AddSingleton(timer);
            services.AddScoped<Lease>();
            services.AddScoped<IRequiredActionProvider>(sp => new DrainingProvider(drain, sp.GetRequiredService<Lease>()));
        });

    private sealed class DrainProbe
    {
        internal DateTimeOffset? ValidUntil { get; init; }
        internal int Calls { get; set; }
        internal TaskCompletionSource InCallback { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource AllowCallbackToFinish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource DisposalStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Drained { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class DrainingProvider(DrainProbe probe, Lease lease) : IRequiredActionProvider, IAsyncDisposable, IDisposable
    {
        private CancellationToken _lifetime;
        private bool _callbackFinished;
        public string CacheConfigurationKey => "invocation-lifetime";
        public TimeSpan Timeout => HeldProvider.Budget;
        public bool FailClosed => true;

        public Task<IReadOnlyList<RequiredAction>> GetPendingAsync(RequiredActionSubject subject, CancellationToken cancellationToken)
        {
            _lifetime = cancellationToken;
            _lifetime.Register(EndInvocation);
            var key = ++probe.Calls == 1 ? "first" : "next";
            IReadOnlyList<RequiredAction> actions = [new()
            {
                Key = key, ResolvePath = "/profile", Scope = RequiredActionScope.User, Severity = RequiredActionSeverity.Blocking
            }];
            return Task.FromResult(probe.ValidUntil is { } deadline ? new RequiredActionReport(actions, deadline) : actions);
        }

        private void EndInvocation()
        {
            probe.InCallback.TrySetResult();
            probe.AllowCallbackToFinish.Task.GetAwaiter().GetResult();
            Assert.False(lease.Disposed.Task.IsCompleted);
            _callbackFinished = true;
        }

        public async ValueTask DisposeAsync()
        {
            probe.DisposalStarted.TrySetResult();
            // Only the actual provider token can drain this dependency.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, _lifetime)
                .WaitAsync(TestGuard, TestContext.Current.CancellationToken));
            Assert.True(_callbackFinished);
            probe.Drained.TrySetResult();
        }

        public void Dispose() { }
    }
}
