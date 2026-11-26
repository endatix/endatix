using System.Security.Cryptography;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>Local, per-registration single flight. A versioned subject epoch retires even the last good report.</summary>
internal sealed class RequiredActionProviderCache(IMemoryCache cache, HybridCache epochs, RequiredActionExecution execution)
{
    private readonly Lock _creation = new();

    internal sealed record Query(Guid Epoch, int Index, RequiredActionSubject Subject, Policy Policy);
    internal sealed record Report(IReadOnlyList<RequiredAction> Actions, DateTime Until, DateTime UsableUntil, bool Unavailable = false)
    {
        internal static Report From(IReadOnlyList<RequiredAction> actions, Policy policy, DateTime now)
        {
            var deadline = actions is RequiredActionReport report ? report.ValidUntil.UtcDateTime : DateTime.MaxValue;
            return new(actions, Earlier(now + policy.CacheFor, deadline), Earlier(now.AddMinutes(1), deadline));
        }

        private static DateTime Earlier(DateTime left, DateTime right) => left < right ? left : right;
    }
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
        internal DateTime RetryAt { get; private set; }

        internal void Forget(Report report) => Interlocked.CompareExchange(ref _lastGood, null, report);

        internal Report Success(Report report)
        {
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
        if (policy.FailClosed || slot.LastGood is not { } good || good.UsableUntil <= now)
        {
            return new([], slot.RetryAt, slot.RetryAt, policy.FailClosed);
        }
        return good with { Until = good.UsableUntil < slot.RetryAt ? good.UsableUntil : slot.RetryAt };
    }
}

internal sealed class RequiredActionExecution(RequiredActionInvocations invocations, ILogger<RequiredActionExecution> logger, IDateTimeProvider clock)
{
    internal DateTime Now => clock.Now.UtcDateTime;

    internal async Task<RequiredActionProviderCache.Report> RefreshAsync(RequiredActionProviderCache.Slot slot,
        RequiredActionProviderCache.Query query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var actions = await invocations.TryReadAsync(query, ct);
            ct.ThrowIfCancellationRequested();
            var now = Now;
            var report = actions is null ? null : RequiredActionProviderCache.Report.From(actions, query.Policy, now);
            return report is not null && now < report.Until ? slot.Success(report) : slot.Failure(query.Policy, now);
        }
        catch (Exception error)
        {
            ct.ThrowIfCancellationRequested();
            logger.LogWarning(error, "Required action provider {Registration} failed; retrying in 15 seconds.", query.Index);
            return slot.Failure(query.Policy, Now);
        }
    }
}
