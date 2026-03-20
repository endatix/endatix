using System.Text.Json.Serialization;
using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure;

namespace Endatix.Infrastructure.Caching;

/// <summary>
/// Immutable envelope containing the data and its caching metadata.
/// </summary>
public sealed record Cached<T> : ICachedData<T> where T : class
{
    [JsonConstructor]
    private Cached(
        T data,
        DateTime cachedAt,
        DateTime expiresAt,
        string? etag)
    {
        Guard.Against.Expression(dateTime => dateTime.Kind != DateTimeKind.Utc, cachedAt, "cachedAt must be in UTC");
        Guard.Against.Expression(dateTime => dateTime.Kind != DateTimeKind.Utc, expiresAt, "expiresAt must be in UTC");
        Guard.Against.Expression(_ => expiresAt < cachedAt, cachedAt, "expiresAt must be greater than or equal to cachedAt");
        Guard.Against.Null(data);

        Data = data;
        CachedAt = cachedAt;
        ExpiresAt = expiresAt;
        ETag = string.IsNullOrWhiteSpace(etag) ? Guid.NewGuid().ToString("N") : etag;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="Cached{T}"/> class.
    /// </summary>
    /// <param name="data">The data to cache.</param>
    /// <param name="utcNow">The UTC date and time the data was cached.</param>
    /// <param name="ttl">The time to live for the cached data.</param>
    /// <param name="etag">The ETag for the cached data.</param>
    public Cached(T data, DateTime utcNow, TimeSpan ttl, string? etag = null)
    {
        Guard.Against.Expression(dateTime => dateTime.Kind != DateTimeKind.Utc, utcNow, "utcNow must be in UTC");
        Guard.Against.Negative(ttl.TotalSeconds);
        Guard.Against.Null(data);

        Data = data;
        CachedAt = utcNow;
        ExpiresAt = utcNow.Add(ttl);
        ETag = string.IsNullOrWhiteSpace(etag) ? Guid.NewGuid().ToString("N") : etag;
    }

    public T Data { get; init; }
    public DateTime CachedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public string ETag { get; init; }

    /// <summary>
    /// Checks if the cached data is expired.
    /// </summary>
    /// <param name="utcNow">The UTC date and time to check if the data is expired.</param>
    /// <returns>True if the data is expired, false otherwise.</returns>
    public bool IsExpired(DateTime utcNow)
    {
        Guard.Against.Expression(dateTime => dateTime.Kind != DateTimeKind.Utc, utcNow, "utcNow must be in UTC");

        return utcNow > ExpiresAt;
    }

    public static Cached<T> Create(T data, DateTime utcNow, TimeSpan ttl, string? etag = null) => new(data, utcNow, ttl, etag);
}
