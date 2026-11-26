using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Infrastructure.Features.AccessControl;

internal sealed record PermissionProjection<T>(string Key, Func<T> Create, string[] Tags) where T : class;

internal sealed class PermissionCacheLifetime(AuthorizationData snapshot, DateTime now)
{
    internal async Task<Result<ICachedData<T>>> ReadAsync<T>(HybridCache cache, PermissionProjection<T> projection, CancellationToken ct)
        where T : class
    {
        var ttl = snapshot.ComputeAuthTtl(now);
        var entry = await cache.GetOrCreateAsync(projection.Key,
            _ => ValueTask.FromResult(new Cached<T>(projection.Create(), now, ttl)),
            new HybridCacheEntryOptions { Expiration = ttl }, tags: projection.Tags, cancellationToken: ct);
        return Result.Success<ICachedData<T>>(Cap(entry));
    }

    // A refreshed snapshot may shorten this response without changing the cached projection.
    private Cached<T> Cap<T>(Cached<T> entry) where T : class
    {
        var deadline = snapshot.ExpiresAt;
        if (deadline == default || deadline >= entry.ExpiresAt)
        {
            return entry;
        }
        // With clock skew, expose an already-expired projection rather than extend the authorization deadline.
        var start = entry.CachedAt < deadline ? entry.CachedAt : deadline;
        return new Cached<T>(entry.Data, start, deadline - start, entry.ETag);
    }
}
