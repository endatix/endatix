using System.Text.Json.Serialization;
using Endatix.Core.Abstractions.Data;

namespace Endatix.Core.Infrastructure.Caching;

/// <summary>
/// Immutable envelope containing the data and its caching metadata.
/// </summary>
public record Cached<T> : ICachedData<T> where T : class
{
    public Cached() { }

    // private Cached() : this(default!, DateTime.UtcNow, DateTime.UtcNow, Guid.NewGuid().ToString("N")) { }
    public Cached(T data, TimeSpan ttl, string? etag = null)
    {
        Data = data;
        CachedAt = DateTime.UtcNow;
        ExpiresAt = DateTime.UtcNow.Add(ttl);
        ETag = etag ?? Guid.NewGuid().ToString("N");
    }

    public T Data { get; init; }
    public DateTime CachedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public string ETag { get; init; }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}
