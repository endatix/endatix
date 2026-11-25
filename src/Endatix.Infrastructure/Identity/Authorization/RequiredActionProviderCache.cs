using System.Security.Cryptography;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>Local, per-registration single flight. A versioned subject epoch retires even the last good report.</summary>
internal sealed class RequiredActionProviderCache(IMemoryCache cache, HybridCache epochs, RequiredActionExecution execution)
{
    private readonly Lock _creation = new();

    internal sealed record Query(Guid Epoch, int Index, RequiredActionSubject Subject, Policy Policy);
    internal sealed record Report(IReadOnlyList<RequiredAction> Actions, DateTime Until, bool Unavailable = false);
    internal sealed record Policy(TimeSpan CacheFor, TimeSpan Timeout, bool FailClosed)
    {
        internal static Policy For(IRequiredActionProvider provider) => new(
            provider.CacheFor > TimeSpan.Zero && provider.CacheFor < TimeSpan.FromMinutes(1) ? provider.CacheFor : TimeSpan.FromMinutes(1),
            provider.Timeout > TimeSpan.Zero ? provider.Timeout : TimeSpan.FromSeconds(2),
            provider.FailClosed);
    }

    internal sealed class Slot
    {
        private Report? _lastGood;
        internal SemaphoreSlim Gate { get; } = new(1);
        internal Report? LastGood => Volatile.Read(ref _lastGood);
        internal DateTime GoodUntil { get; private set; }
        internal DateTime RetryAt { get; private set; }
        internal Task? Running { get; set; }

        internal void Forget(Report report) => Interlocked.CompareExchange(ref _lastGood, null, report);

        internal Report Success(Report report, DateTime now)
        {
            GoodUntil = now.AddMinutes(1);
            Volatile.Write(ref _lastGood, report);
            return report;
        }

        internal Report Failure(Policy policy, DateTime now)
        {
            RetryAt = now.AddSeconds(15);
            return Fallback(this, policy, now);
        }
    }

    internal ValueTask<Guid> SubjectEpochAsync(RequiredActionSubject subject, AuthorizationCache.CacheEntry entry, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { subject.UserId, subject.TenantId, Roles = subject.Roles.Order(StringComparer.Ordinal) });
        var key = $"{entry.Versions.Prefix}:{entry.Versions.Versions}:standing:{entry.Configuration}:{Convert.ToHexString(SHA256.HashData(bytes))}";
        return epochs.GetOrCreateAsync(key, _ => ValueTask.FromResult(Guid.NewGuid()),
            new HybridCacheEntryOptions { Flags = HybridCacheEntryFlags.DisableDistributedCache, LocalCacheExpiration = TimeSpan.FromMinutes(5) },
            entry.Tags, ct);
    }

    private Slot GetSlot(Query query)
    {
        lock (_creation)
        {
            return cache.GetOrCreate((query.Epoch, query.Index), entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(2);
                return new Slot();
            })!;
        }
    }

    internal void ForgetContradiction(Query query, Report report) =>
        GetSlot(query).Forget(report);

    internal async Task<Report> ReadAsync(Query query, CancellationToken ct)
    {
        var slot = GetSlot(query);
        await slot.Gate.WaitAsync(ct);
        try
        {
            var now = execution.Now;
            if (slot.RetryAt > now)
            {
                return Fallback(slot, query.Policy, now);
            }
            return slot.LastGood is { } good && good.Until > now
                ? good : await execution.RefreshAsync(slot, query, ct);
        }
        finally
        {
            slot.Gate.Release();
        }
    }

    internal static Report Fallback(Slot slot, Policy policy, DateTime now)
    {
        if (policy.FailClosed || slot.LastGood is not { } good || slot.GoodUntil <= now)
        {
            return new([], slot.RetryAt, policy.FailClosed);
        }
        return good with { Until = slot.GoodUntil < slot.RetryAt ? slot.GoodUntil : slot.RetryAt };
    }
}

internal sealed class RequiredActionExecution(IServiceScopeFactory scopes, ILogger<RequiredActionExecution> logger, IDateTimeProvider clock)
{
    internal DateTime Now => clock.Now.UtcDateTime;

    internal async Task<RequiredActionProviderCache.Report> RefreshAsync(RequiredActionProviderCache.Slot slot,
        RequiredActionProviderCache.Query query, CancellationToken ct)
    {
        if (slot.Running is { IsCompleted: false })
        {
            return slot.Failure(query.Policy, Now);
        }
        try
        {
            var actions = await ReadWithinBudgetAsync(slot, query, ct);
            var now = Now;
            return slot.Success(new(actions, now + query.Policy.CacheFor), now);
        }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(error, "Required action provider {Registration} failed; retrying in 15 seconds.", query.Index);
            return slot.Failure(query.Policy, Now);
        }
    }

    private async Task<IReadOnlyList<RequiredAction>> ReadWithinBudgetAsync(RequiredActionProviderCache.Slot slot,
        RequiredActionProviderCache.Query query, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(query.Policy.Timeout);
        var token = timeout.Token;
        var work = Task.Run(() => InvokeAsync(query, token), token);
        slot.Running = work;
        // A provider that ignores cancellation still owns its scope and may fault after our waiter has returned.
        _ = work.ContinueWith(t => _ = t.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return await work.WaitAsync(token);
    }

    private async Task<IReadOnlyList<RequiredAction>> InvokeAsync(RequiredActionProviderCache.Query query, CancellationToken token)
    {
        // Each concurrent call owns its scoped dependencies until it actually finishes, even past its timeout.
        await using var scope = scopes.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetServices<IRequiredActionProvider>().ElementAt(query.Index);
        return await provider.GetPendingAsync(query.Subject, token);
    }
}
