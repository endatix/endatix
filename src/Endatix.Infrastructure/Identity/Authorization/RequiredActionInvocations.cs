using System.Collections.Concurrent;
using Endatix.Core.Abstractions.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Endatix.Infrastructure.Identity.Authorization.RequiredActionProviderCache;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>Capacity follows actual work on this host, not the cache generation that started it.</summary>
internal sealed class RequiredActionInvocations(IServiceScopeFactory scopes, ILogger<RequiredActionInvocations> logger, TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly ConcurrentDictionary<CapacityKey, byte> _active = new();
    private readonly record struct CapacityKey(string UserId, long TenantId, int Registration);

    internal async Task<IReadOnlyList<RequiredAction>?> TryReadAsync(Query query, CancellationToken ct)
    {
        var started = _time.GetTimestamp();
        using var deadline = new CancellationTokenSource(query.Policy.Timeout, _time);
        using var waiter = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, ct);
        var key = new CapacityKey(query.Subject.UserId, query.Subject.TenantId, query.Index);
        if (!_active.TryAdd(key, 0))
        {
            return null;
        }
        ProviderCancellation provider = new();
        var work = Task.Run(() => InvokeAsync(query, key, provider), CancellationToken.None);
        var actions = await WaitAsync(work, provider, waiter.Token);
        ct.ThrowIfCancellationRequested();
        return _time.GetElapsedTime(started) < query.Policy.Timeout ? actions : throw new TimeoutException();
    }

    private async Task<IReadOnlyList<RequiredAction>> InvokeAsync(Query query, CapacityKey key, ProviderCancellation provider)
    {
        try
        {
            return await ReadInScopeAsync(query, provider);
        }
        finally
        {
            provider.Dispose();
            _active.TryRemove(key, out _);
        }
    }

    private async Task<IReadOnlyList<RequiredAction>> ReadInScopeAsync(Query query, ProviderCancellation provider)
    {
        await using var scope = scopes.CreateAsyncScope();
        try
        {
            provider.Token.ThrowIfCancellationRequested();
            var source = scope.ServiceProvider.GetServices<IRequiredActionProvider>().ElementAt(query.Index);
            return await source.GetPendingAsync(query.Subject, provider.Token);
        }
        finally
        {
            await provider.EndInvocationAsync();
        }
    }

    private async Task<IReadOnlyList<RequiredAction>> WaitAsync(Task<IReadOnlyList<RequiredAction>> work, ProviderCancellation provider, CancellationToken waiter)
    {
        try
        {
            return await work.WaitAsync(waiter);
        }
        catch (OperationCanceledException) when (waiter.IsCancellationRequested)
        {
            _ = provider.EndInvocationAsync();
            _ = ObserveLateFaultAsync(work);
            throw;
        }
    }

    private async Task ObserveLateFaultAsync(Task work)
    {
        try
        {
            await work;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning(error, "Required action provider failed after the request stopped waiting.");
        }
    }

    /// <summary>The waiter can signal cancellation but never runs or joins provider callbacks.</summary>
    private sealed class ProviderCancellation : IDisposable
    {
        private readonly CancellationTokenSource _source = new();
        private readonly Lock _gate = new();
        private Task? _callbacks;
        internal CancellationToken Token => _source.Token;

        internal Task EndInvocationAsync()
        {
            lock (_gate)
            {
                return _callbacks ??= _source.CancelAsync();
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                // Scope creation itself can fail before the provider receives a token.
                _callbacks ??= Task.CompletedTask;
                _source.Dispose();
            }
        }
    }
}
