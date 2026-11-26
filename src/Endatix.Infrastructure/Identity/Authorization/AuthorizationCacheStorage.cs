using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>Fences fills that outlive a tag eviction. Access versions remain the durable authority.</summary>
internal sealed class AuthorizationCacheStorage(HybridCache entries, IDistributedCache? distributed = null)
{
    internal HybridCache Entries => entries;
    private static readonly ConditionalWeakTable<HybridCache, SemaphoreSlim[]> _cacheGates = new();
    private readonly SemaphoreSlim[] _gates = _cacheGates.GetValue(entries, static _ => Enumerable.Range(0, 32).Select(_ => new SemaphoreSlim(1)).ToArray());
    private static readonly TimeSpan _lifetime = TimeSpan.FromMinutes(5);
    private static readonly HybridCacheEntryOptions _localMarker = new()
    {
        Flags = HybridCacheEntryFlags.DisableDistributedCache,
        LocalCacheExpiration = _lifetime
    };
    private const string ALL_USERS = "endatix:authorization:generation:all";
    private static string User(string userId) =>
        $"endatix:authorization:generation:user:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId)))}";

    internal async ValueTask<string> GenerationAsync(string userId, CancellationToken ct) =>
        $"{await ReadGenerationAsync(ALL_USERS, ct):N}{await ReadGenerationAsync(User(userId), ct):N}";

    internal Task RetireUserAsync(string userId, CancellationToken ct) => RetireAsync(User(userId), ct);
    internal Task RetireAllAsync(CancellationToken ct) => RetireAsync(ALL_USERS, ct);

    private async ValueTask<Guid> ReadGenerationAsync(string key, CancellationToken ct)
    {
        if (distributed is not null && await distributed.GetAsync(key, ct) is { } bytes)
        {
            return new(bytes);
        }
        var gate = GateFor(key);
        await gate.WaitAsync(ct);
        try
        {
            // Complete local publication under the gate, even if the reader leaves.
            return distributed is null
                ? await entries.GetOrCreateAsync(key, static _ => ValueTask.FromResult(Guid.NewGuid()), _localMarker, cancellationToken: CancellationToken.None)
                : await InitializeAsync(key, distributed, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private SemaphoreSlim GateFor(string key) => _gates[(uint)StringComparer.Ordinal.GetHashCode(key) % _gates.Length];

    private static async Task<Guid> InitializeAsync(string key, IDistributedCache store, CancellationToken ct)
    {
        var existing = await store.GetAsync(key, ct);
        if (existing is not null)
        {
            return new(existing);
        }
        var fresh = Guid.NewGuid();
        await store.SetAsync(key, fresh.ToByteArray(), new() { AbsoluteExpirationRelativeToNow = _lifetime }, ct);
        return fresh;
    }

    private async Task RetireAsync(string key, CancellationToken ct)
    {
        var gate = GateFor(key);
        await gate.WaitAsync(ct);
        try
        {
            if (distributed is null)
            {
                await entries.RemoveAsync(key, ct);
            }
            else
            {
                await distributed.RemoveAsync(key, ct);
            }
        }
        finally
        {
            gate.Release();
        }
    }
}
