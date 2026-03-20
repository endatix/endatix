using Microsoft.Extensions.Caching.Hybrid;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Infrastructure;

namespace Endatix.Infrastructure.Caching;

internal static class HybridCacheExtensions
{
    /// <summary>
    /// Options to disable cache write operations for HybridCache.
    /// </summary>
    private static readonly HybridCacheEntryOptions _disableCacheWriteoptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite | HybridCacheEntryFlags.DisableDistributedCacheWrite
    };

    /// <summary>
    /// Options to disable cache create factory operations for HybridCache making it effectively "read-only".
    /// </summary>
    private static readonly HybridCacheEntryOptions _disableCreateFactoryOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableUnderlyingData
    };

    /// <summary>
    /// Gets a cached value if it exists; otherwise returns <c>null</c> without invoking the factory.
    /// </summary>
    /// <remarks>
    /// This relies on <see cref="HybridCacheEntryFlags.DisableUnderlyingData"/> so HybridCache becomes effectively "read-only".
    /// </remarks>
    public static async Task<T?> GetOrDefaultAsync<T>(this HybridCache cache, string key, CancellationToken cancellationToken)
        where T : class => await cache.GetOrCreateAsync(
            key,
            _ => ValueTask.FromResult<T?>(null),
            _disableCreateFactoryOptions,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Gets a cached value if it exists; otherwise invokes the factory and caches only if the result is successful.
    /// </summary>
    /// <typeparam name="T">The type of the value of the item in the cache.</typeparam>
    /// <param name="cache">An instance of <see cref="HybridCache"/></param>
    /// <param name="key">The name (key) of the item to search for in the cache.</param>
    /// <param name="factory">The factory to invoke if the item is not found in the cache.</param>
    /// <param name="options">The cache entry options.</param>
    /// <param name="tags">The tags to associate with the cache entry.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cached or newly created result.</returns>
    public static async Task<Result<T>> GetOrCreateResultAsync<T>(
        this HybridCache cache,
        string key,
        Func<CancellationToken, Task<Result<T>>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cachedOrCreated = await cache.GetOrCreateAsync(
                key,
                async ct =>
                {
                    var result = await factory(ct);
                    if (!result.IsSuccess)
                    {
                        throw new FailedResultException<T>(result);
                    }

                    return result.Value;
                },
                options,
                tags,
                cancellationToken);

            return Result.Success(cachedOrCreated);
        }
        catch (FailedResultException<T> ex)
        {
            return ex.Result;
        }
    }

    /// <summary>
    /// Result wrapper for <see cref="HybridCache"/> allowing to only cache if the Result is successful.
    /// Gets a cached <see cref="Cached{T}"/> envelope if it exists; otherwise invokes the factory, wraps the successful result in a <see cref="Cached{T}"/> envelope, and caches it.
    /// <param name="cache">An instance of <see cref="HybridCache"/></param>
    /// <param name="key">The name (key) of the item to search for in the cache.</param>
    /// <param name="factory">The factory to invoke if the item is not found in the cache.</param>
    /// <param name="ttl">The time to live for the cached data.</param>
    /// <param name="utcNow">The UTC date and time the data was cached.</param>
    /// <param name="tags">The tags to associate with the cache entry.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cached or newly created result.</returns>
    /// </summary>
    /// <typeparam name="T">The type of the value of the item in the cache.</typeparam>
    public static async Task<Result<ICachedData<T>>> GetOrCreateCachedResultAsync<T>(
        this HybridCache cache,
        string key,
        Func<CancellationToken, Task<Result<T>>> factory,
        TimeSpan ttl,
        DateTime utcNow,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        try
        {
            var cachedOrCreated = await cache.GetOrCreateAsync(
                key,
                async ct =>
                {
                    var result = await factory(ct);
                    if (!result.IsSuccess)
                    {
                        throw new FailedResultException<T>(result);
                    }

                    return Cached<T>.Create(result.Value, utcNow, ttl);
                },
                new HybridCacheEntryOptions { Expiration = ttl },
                tags,
                cancellationToken);

            return Result.Success(cachedOrCreated);
        }
        catch (FailedResultException<T> ex)
        {
            return ex.Result.ToErrorResult<ICachedData<T>>();
        }
    }

    /// <summary>
    /// Returns true if the cache contains an item with a matching key, along with the value of the matching cache entry.
    /// </summary>
    /// <typeparam name="T">The type of the value of the item in the cache.</typeparam>
    /// <param name="cache">An instance of <see cref="HybridCache"/></param>
    /// <param name="key">The name (key) of the item to search for in the cache.</param>
    /// <param name="cancellation">The cancellation token to cancel the operation.</param>
    /// <returns>A tuple of <see cref="bool"/> and the object (if found) retrieved from the cache.</returns>
    /// <remarks>Will never add or alter the state of any items in the cache.</remarks>
    public static async Task<(bool, T?)> TryGetValueAsync<T>(this HybridCache cache, string key, CancellationToken cancellation)
    {
        var exists = true;

        var result = await cache.GetOrCreateAsync<object, T>(
            key,
            null!,
            (_, _) =>
            {
                exists = false;
                return new ValueTask<T>(default(T)!);
            },
            _disableCacheWriteoptions,
            null,
            cancellation);

        return (exists, result);
    }
}