using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>Fences fills that outlive a tag eviction. Access versions remain the durable authority.</summary>
internal sealed class AuthorizationCacheStorage(HybridCache entries, IMemoryCache memory, IDistributedCache? distributed = null)
{
    internal HybridCache Entries => entries;
    private readonly Lock _local = new();
    private readonly SemaphoreSlim[] _initialization = Enumerable.Range(0, 32).Select(_ => new SemaphoreSlim(1)).ToArray();
    private static readonly TimeSpan _lifetime = TimeSpan.FromMinutes(5);
    private const string ALL_USERS = "endatix:authorization:generation:all";
    private static string User(string userId) => $"endatix:authorization:generation:user:{userId}";

    internal async ValueTask<string> GenerationAsync(string userId, CancellationToken ct) =>
        $"{await ReadGenerationAsync(ALL_USERS, ct):N}{await ReadGenerationAsync(User(userId), ct):N}";

    internal Task RetireUserAsync(string userId, CancellationToken ct) => RetireAsync(User(userId), ct);
    internal Task RetireAllAsync(CancellationToken ct) => RetireAsync(ALL_USERS, ct);

    private async ValueTask<Guid> ReadGenerationAsync(string key, CancellationToken ct)
    {
        if (distributed is null)
        {
            return LocalGeneration(key);
        }
        var bytes = await distributed.GetAsync(key, ct);
        if (bytes is not null)
        {
            return new Guid(bytes);
        }
        return await InitializeAsync(key, distributed, ct);
    }

    private async Task<Guid> InitializeAsync(string key, IDistributedCache store, CancellationToken ct)
    {
        var gate = _initialization[(uint)StringComparer.Ordinal.GetHashCode(key) % _initialization.Length];
        await gate.WaitAsync(ct);
        try
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
        finally
        {
            gate.Release();
        }
    }

    private Guid LocalGeneration(string key)
    {
        lock (_local)
        {
            return memory.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _lifetime;
                entry.Size = 1;
                return Guid.NewGuid();
            });
        }
    }

    private Task RetireAsync(string key, CancellationToken ct)
    {
        if (distributed is not null)
        {
            return distributed.RemoveAsync(key, ct);
        }
        lock (_local)
        {
            memory.Remove(key);
        }
        return Task.CompletedTask;
    }
}
