using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public sealed partial class AuthorizationCacheHybridTests
{
    [Fact]
    public async Task GetOrCreateAsync_ProviderCleanupIsHeld_LateClearanceDoesNotAdmitNewGeneration()
    {
        // Arrange
        var timer = new FakeTimeProvider();
        var cleanup = new CleanupControl();
        var cache = CreateInstance(false, services =>
        {
            services.AddSingleton<TimeProvider>(timer);
            services.AddScoped<IRequiredActionProvider>(_ => new SlowCleanupProvider(cleanup));
        });
        var read = SnapshotAsync(cache);
        await cleanup.Entered.Task.WaitAsync(TestGuard, TestContext.Current.CancellationToken);

        // Act / Assert
        try
        {
            timer.Advance(HeldProvider.Budget);
            (await AwaitAsync(read)).RequiredActionsUnavailable.Should().BeTrue();
            _versionStore.SystemVersion = Guid.NewGuid();
            (await AwaitAsync(SnapshotAsync(cache))).RequiredActionsUnavailable.Should().BeTrue();
            cleanup.Calls.Should().Be(1);
        }
        finally
        {
            cleanup.Release.TrySetResult();
        }
        await cleanup.Disposed.Task.WaitAsync(TestGuard, TestContext.Current.CancellationToken);
        (await AwaitAsync(SnapshotAsync(cache))).RequiredActionsUnavailable.Should().BeTrue();
    }

    private sealed class CleanupControl
    {
        internal int Calls { get; set; }
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class SlowCleanupProvider(CleanupControl cleanup) : IRequiredActionProvider, IDisposable, IAsyncDisposable
    {
        public TimeSpan Timeout => HeldProvider.Budget;
        public bool FailClosed => true;
        public string CacheConfigurationKey => "slow-cleanup";

        public Task<IReadOnlyList<RequiredAction>> GetPendingAsync(RequiredActionSubject subject, CancellationToken cancellationToken)
        {
            cleanup.Calls++;
            return Task.FromResult<IReadOnlyList<RequiredAction>>([]);
        }

        public async ValueTask DisposeAsync()
        {
            cleanup.Entered.TrySetResult();
            await cleanup.Release.Task;
            Dispose();
        }

        public void Dispose() => cleanup.Disposed.TrySetResult();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task GetOrCreateAsync_ProviderCallbackIsHeld_ReturnsAndBoundsWorkAcrossRevisions(bool mandatory, bool cancelRequest)
    {
        var timer = new FakeTimeProvider();
        var remote = new HeldRemote(mandatory);
        var late = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = CreateHeldCache(timer, remote, late);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var read = cache.GetOrCreateAsync(CreatePrincipal(), _ => Task.FromResult(Result.Success(CreateData(default))), caller.Token);
        var lease = await AwaitAsync(remote.Started.Task);
        try
        {
            if (cancelRequest)
            {
                await CancelReadAsync(caller, read);
            }
            else
            {
                timer.Advance(HeldProvider.Budget);
                (await AwaitAsync(read)).RequiredActionsUnavailable.Should().Be(mandatory);
            }
            await remote.CallbackEntered.Task.WaitAsync(TestGuard, TestContext.Current.CancellationToken);
            remote.Token.WaitHandle.WaitOne(0).Should().BeTrue();
            remote.Finish.SetException(new HttpRequestException("Late provider failure"));
            await remote.Leaving.Task.WaitAsync(TestGuard, TestContext.Current.CancellationToken);
            lease.Disposed.Task.IsCompleted.Should().BeFalse();
            await CheckOccupiedAsync(cache, remote);
        }
        finally
        {
            remote.ReleaseCallback.TrySetResult();
            remote.Finish.TrySetResult([]);
        }
        await lease.Disposed.Task.WaitAsync(TestGuard, TestContext.Current.CancellationToken);
        (await AwaitAsync(late.Task)).Should().BeOfType<HttpRequestException>();
        _versionStore.TenantVersions[42] = Guid.NewGuid();
        (await AwaitAsync(SnapshotAsync(cache))).RequiredActions.Should().ContainSingle().Which.Key.Should().Be("fresh");
        remote.Calls.Should().Be(2);
    }

    private AuthorizationCache CreateHeldCache(TimeProvider timer, HeldRemote remote, TaskCompletionSource<Exception> late) =>
        CreateInstance(false, services =>
        {
            services.AddSingleton(timer);
            var logger = Substitute.For<ILogger<RequiredActionInvocations>>();
            logger.WhenForAnyArgs(log => log.Log<Arg.AnyType>(default, default, default!, default, default!))
                .Do(call => late.TrySetResult(call.Arg<Exception>()));
            services.AddSingleton(logger);
            services.AddScoped<Lease>();
            services.AddScoped<IRequiredActionProvider>(sp => new HeldProvider(remote, sp.GetRequiredService<Lease>()));
        });

    private static async Task CancelReadAsync(CancellationTokenSource caller, Task<AuthorizationData> read)
    {
        await caller.CancelAsync().WaitAsync(TestGuard, TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AwaitAsync(read));
    }

    private static TimeSpan TestGuard { get; } = TimeSpan.FromSeconds(5);
    private static Task<T> AwaitAsync<T>(Task<T> task) => task.WaitAsync(TestGuard, TestContext.Current.CancellationToken);

    private async Task CheckOccupiedAsync(AuthorizationCache cache, HeldRemote remote)
    {
        for (var revision = 0; revision < 3; revision++)
        {
            if (revision == 1)
            {
                _versionStore.SystemVersion = Guid.NewGuid();
            }
            else
            {
                _versionStore.TenantVersions[42] = Guid.NewGuid();
            }
            var occupied = await AwaitAsync(SnapshotAsync(cache));
            occupied.RequiredActionsUnavailable.Should().Be(remote.Mandatory);
            occupied.RequiredActions.Should().BeEmpty();
        }
        remote.Calls.Should().Be(1);
        var neighbor = CreateData(default) with { UserId = "456" };
        var independent = await cache.GetOrCreateAsync(neighbor.UserId, neighbor.TenantId, null,
            _ => Task.FromResult(Result.Success(neighbor)), TestContext.Current.CancellationToken);
        independent.RequiredActions.Should().ContainSingle().Which.Key.Should().Be("fresh");
    }

    private sealed class HeldRemote(bool mandatory)
    {
        internal bool Mandatory { get; } = mandatory;
        internal int Calls => _calls;
        private int _calls;
        internal TaskCompletionSource<Lease> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource CallbackEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseCallback { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<IReadOnlyList<RequiredAction>> Finish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Leaving { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationToken Token { get; private set; }

        internal async Task<IReadOnlyList<RequiredAction>> ReadAsync(RequiredActionSubject subject, Lease lease, CancellationToken token)
        {
            if (subject.UserId != "123" || Interlocked.Increment(ref _calls) != 1)
            {
                return [new() { Key = "fresh", ResolvePath = "/fresh", Scope = RequiredActionScope.User, Severity = RequiredActionSeverity.Blocking }];
            }
            Token = token;
            token.Register(HoldCallback);
            Started.TrySetResult(lease);
            try
            {
                return await Finish.Task;
            }
            finally
            {
                Leaving.TrySetResult();
            }
        }

        private void HoldCallback()
        {
            CallbackEntered.TrySetResult();
            ReleaseCallback.Task.GetAwaiter().GetResult();
        }
    }

    private sealed class HeldProvider(HeldRemote remote, Lease lease) : IRequiredActionProvider
    {
        internal static TimeSpan Budget { get; } = TimeSpan.FromMinutes(1);
        public string CacheConfigurationKey => "held";
        public bool FailClosed => remote.Mandatory;
        public TimeSpan Timeout => Budget;
        public Task<IReadOnlyList<RequiredAction>> GetPendingAsync(RequiredActionSubject subject, CancellationToken cancellationToken) =>
            remote.ReadAsync(subject, lease, cancellationToken);
    }

}
