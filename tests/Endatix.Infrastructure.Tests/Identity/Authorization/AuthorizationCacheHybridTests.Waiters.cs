using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Infrastructure.Multitenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public sealed partial class AuthorizationCacheHybridTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetOrCreateAsync_GrantReadCrossesInvalidation_NextRequestReadsCurrentGrants(bool changeVersion)
    {
        var cache = CreateInstance(false);
        var first = new GrantRequest(cache, TestContext.Current.CancellationToken);
        var read = first.TransformAsync();
        await AwaitAsync(first.Started.Task);
        if (changeVersion)
        {
            _versionStore.SystemVersion = Guid.NewGuid();
        }
        else
        {
            await cache.InvalidateAsync("123", 42, TestContext.Current.CancellationToken);
        }
        first.Finish.SetResult(CreateData(default));
        await AwaitAsync(read);

        var next = new GrantRequest(cache, TestContext.Current.CancellationToken);
        next.Finish.SetResult(CreateData(default) with { Roles = [] });
        (await AwaitAsync(next.TransformAsync())).IsInRole("Creator").Should().BeFalse();
        next.Calls.Should().Be(1);
    }

    [Fact]
    public async Task TransformAsync_AbortedRequest_PropagatesCancellationWithoutLoggingAnError()
    {
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        var http = new DefaultHttpContext { RequestAborted = aborted.Token };
        var principal = CreatePrincipal();
        var cache = Substitute.For<IAuthorizationCache>();
        var logger = Substitute.For<ILogger<ClaimsTransformer>>();
        var strategy = Substitute.For<IAuthorizationStrategy>();
        strategy.CanHandle(principal).Returns(true);
        cache.GetOrCreateAsync(principal, Arg.Any<Func<CancellationToken, Task<Result<AuthorizationData>>>>(),
            aborted.Token).Returns(Task.FromCanceled<AuthorizationData>(aborted.Token));
        var transformer = new ClaimsTransformer([strategy], cache, logger, Options.Create(new EndatixJwtOptions()),
            new HttpContextAccessor { HttpContext = http });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transformer.TransformAsync(principal));

        logger.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task TransformAsync_InitiatorLeavesSharedEvaluation_OnlyCancelsWithoutASurvivor(bool join, bool expired)
    {
        using var abort = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var remote = new ProviderRead(expired);
        await using var services = WaiterServices(remote);
        using var firstScope = services.CreateScope();
        var first = new GrantRequest(firstScope.ServiceProvider.GetRequiredService<AuthorizationCache>(), abort.Token);
        var grants = CreateData(default);
        var departing = first.TransformAsync(grants);
        var provider = await AwaitAsync(remote.Started.Task);
        using var secondScope = services.CreateScope();
        var second = new GrantRequest(secondScope.ServiceProvider.GetRequiredService<AuthorizationCache>(), TestContext.Current.CancellationToken);
        var survivor = join ? second.TransformAsync(CreateData(default)) : null;
        try
        {
            if (join)
            {
                await AwaitAsync(remote.Joined.Task);
            }
            await abort.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AwaitAsync(departing));
            firstScope.Dispose();
            grants.Roles.Clear();
            if (survivor is not null)
            {
                provider.IsDisposed.Should().BeFalse();
                remote.Finish.SetResult([]);
                (await AwaitAsync(survivor)).IsInRole("Creator").Should().BeTrue();
            }
            else
            {
                await AwaitAsync(remote.Canceled.Task);
            }
            remote.Calls.Should().Be(1);
        }
        finally
        {
            remote.Finish.TrySetResult([]);
        }
    }

    private ServiceProvider WaiterServices(ProviderRead remote)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddHybridCache();
        services.AddSingleton(_clock);
        services.AddSingleton<IAccessVersionStore>(_versionStore);
        services.AddHttpContextAccessor();
        services.AddOptions<EndatixJwtOptions>().Configure(o => o.SigningKey = "test-signing-key-32-characters");
        services.AddScoped<AuthorizationTokenFingerprint>();
        services.AddSingleton(sp => new AuthorizationCacheStorage(remote.Observe(sp.GetRequiredService<HybridCache>()), sp.GetRequiredService<IMemoryCache>()));
        services.AddSingleton<RequiredActionProviderCache>();
        services.AddSingleton<RequiredActionExecution>();
        services.AddSingleton<RequiredActionInvocations>();
        services.AddScoped<RequiredActionEvaluation>();
        services.AddScoped<IRequiredActionProvider>(_ => new WaiterProvider(remote));
        services.AddScoped<AuthorizationCache>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed class ProviderRead(bool expired)
    {
        internal TaskCompletionSource<bool> Joined { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<WaiterProvider> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<IReadOnlyList<RequiredAction>> Finish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Calls => _calls;
        private int _calls;
        internal void StartedCall() => Interlocked.Increment(ref _calls);
        private int _waiters;

        internal HybridCache Observe(HybridCache cache)
        {
            var forward = Substitute.For<HybridCache>();
            forward.GetOrCreateAsync(Arg.Any<string>(), 0, Arg.Any<Func<int, CancellationToken, ValueTask<AuthorizationData?>>>(),
                Arg.Any<HybridCacheEntryOptions>(), Arg.Any<string[]>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult<AuthorizationData?>(expired ? CreateData(DateTime.UnixEpoch) : null));
            forward.GetOrCreateAsync(Arg.Any<string>(), Arg.Any<Func<CancellationToken, ValueTask<AuthorizationData>>>(),
                Arg.Any<HybridCacheEntryOptions>(), Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                var waiter = cache.GetOrCreateAsync(call.Arg<string>(), call.Arg<Func<CancellationToken, ValueTask<AuthorizationData>>>(),
                    call.Arg<HybridCacheEntryOptions>(), call.Arg<string[]>(), call.Arg<CancellationToken>());
                if (Interlocked.Increment(ref _waiters) == 2)
                {
                    Joined.SetResult(true);
                }
                return waiter;
            });
            return forward;
        }
    }

    private sealed class WaiterProvider(ProviderRead remote) : IRequiredActionProvider, IDisposable
    {
        public string CacheConfigurationKey => "shared";
        public TimeSpan Timeout => TimeSpan.FromMinutes(1);
        internal bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;

        public async Task<IReadOnlyList<RequiredAction>> GetPendingAsync(RequiredActionSubject subject, CancellationToken cancellationToken)
        {
            remote.StartedCall();
            remote.Started.SetResult(this);
            try
            {
                var result = await remote.Finish.Task.WaitAsync(cancellationToken);
                ObjectDisposedException.ThrowIf(IsDisposed, this);
                return result;
            }
            catch (OperationCanceledException)
            {
                remote.Canceled.TrySetResult(true);
                throw;
            }
        }
    }

    [Fact]
    public async Task TransformAsync_FirstGrantReadAborts_SurvivorRetainsItsRequestAndWarmsCache()
    {
        var cache = CreateInstance(false);
        using var abort = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var first = new GrantRequest(cache, abort.Token);
        var survivor = new GrantRequest(cache, TestContext.Current.CancellationToken);

        var departing = first.TransformAsync();
        await AwaitAsync(first.Started.Task);
        var remaining = survivor.TransformAsync();
        try
        {
            await AwaitAsync(survivor.Started.Task);
            await abort.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AwaitAsync(departing));
            survivor.Finish.SetResult(CreateData(default));
            (await AwaitAsync(remaining)).IsInRole("Creator").Should().BeTrue();
            (await survivor.TransformAsync()).IsInRole("Creator").Should().BeTrue();
            survivor.Calls.Should().Be(1);
            first.Calls.Should().Be(1);
            TenantAccessDenial.IsMarked(first.Http).Should().BeTrue();
            TenantAccessDenial.IsMarked(survivor.Http).Should().BeFalse();
        }
        finally
        {
            await abort.CancelAsync();
            first.Finish.TrySetResult(CreateData(default));
            survivor.Finish.TrySetResult(CreateData(default));
        }
    }

    private sealed class GrantRequest : IAuthorizationStrategy
    {
        private readonly ClaimsTransformer _transformer;
        private readonly HttpContextAccessor _accessor = new();
        internal DefaultHttpContext Http { get; }
        internal TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<AuthorizationData> Finish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Calls { get; private set; }

        internal GrantRequest(AuthorizationCache cache, CancellationToken token)
        {
            Http = new() { RequestAborted = token };
            _transformer = new([this], cache, NullLogger<ClaimsTransformer>.Instance, Options.Create(new EndatixJwtOptions()), _accessor);
        }

        internal Task<ClaimsPrincipal> TransformAsync(AuthorizationData grants)
        {
            Finish.SetResult(grants);
            return TransformAsync();
        }

        internal async Task<ClaimsPrincipal> TransformAsync()
        {
            _accessor.HttpContext = Http;
            try
            {
                return await _transformer.TransformAsync(CreatePrincipal());
            }
            finally
            {
                _accessor.HttpContext = null;
            }
        }

        public bool CanHandle(ClaimsPrincipal principal) => true;

        public async Task<Result<AuthorizationData>> GetAuthorizationDataAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
        {
            Calls++;
            Started.TrySetResult(true);
            try
            {
                return Result.Success(await Finish.Task.WaitAsync(cancellationToken));
            }
            catch (OperationCanceledException)
            {
                TenantAccessDenial.Mark(_accessor.HttpContext);
                throw;
            }
        }
    }
}
