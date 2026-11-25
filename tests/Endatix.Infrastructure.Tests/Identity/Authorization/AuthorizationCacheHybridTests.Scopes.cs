using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public sealed partial class AuthorizationCacheHybridTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetOrCreateAsync_AbandonedInvocation_OwnsItsScopeUntilCompletion(bool cancelRequest)
    {
        // Arrange
        var slow = new Invocation(cancelRequest ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(1));
        var fast = new Invocation(TimeSpan.FromSeconds(10));
        fast.Finish.SetResult([]);
        var cache = CreateInstance(false, services =>
        {
            services.AddScoped<Lease>();
            slow.Register(services);
            fast.Register(services);
        });
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var read = cache.GetOrCreateAsync(CreatePrincipal(), _ => Task.FromResult(Result.Success(CreateData(default))), source.Token);
        var lease = await slow.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), source.Token);
        var other = await fast.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), source.Token);

        // Act
        if (cancelRequest)
        {
            await source.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
        }
        else
        {
            (await read).RequiredActionsUnavailable.Should().BeTrue();
        }

        // Assert
        lease.Should().NotBeSameAs(other);
        lease.Disposed.Task.IsCompleted.Should().BeFalse();
        slow.Finish.SetResult([]);
        await lease.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await other.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        slow.Subject!.UserId.Should().Be("123");
        slow.Subject.TenantId.Should().Be(42);
        slow.Subject.Roles.Should().Contain("Creator");
        slow.Offline = true;
        (await SnapshotAsync(cache)).RequiredActionsUnavailable.Should().BeTrue();
    }

    private sealed class Lease : IDisposable
    {
        internal TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Dispose() => Disposed.TrySetResult();
    }

    private sealed class Invocation(TimeSpan timeout)
    {
        internal TaskCompletionSource<Lease> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<IReadOnlyList<RequiredAction>> Finish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal RequiredActionSubject? Subject { get; private set; }
        internal bool Offline { get; set; }

        internal void Register(IServiceCollection services) =>
            services.AddScoped<IRequiredActionProvider>(sp => new ScopedProvider(this, sp.GetRequiredService<Lease>(), timeout));

        private sealed class ScopedProvider(Invocation call, Lease lease, TimeSpan timeout) : IRequiredActionProvider
        {
            public string CacheConfigurationKey => "scoped";
            public TimeSpan Timeout => timeout;
            public bool FailClosed => true;

            public Task<IReadOnlyList<RequiredAction>> GetPendingAsync(RequiredActionSubject subject, CancellationToken cancellationToken)
            {
                call.Subject = subject;
                call.Started.TrySetResult(lease);
                return call.Offline ? throw new HttpRequestException() : call.Finish.Task;
            }
        }
    }
}
