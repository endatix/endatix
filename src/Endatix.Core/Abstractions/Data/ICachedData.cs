namespace Endatix.Core.Abstractions.Data;

/// <summary>
/// Interface for cached data.
/// </summary>
public interface ICachedData
{
    /// <summary>
    /// The date and time the data was cached.
    /// </summary>
    DateTime CachedAt { get; init; }

    /// <summary>
    /// The date and time the data will expire.
    /// </summary>
    DateTime ExpiresAt { get; init; }

    /// <summary>
    /// The ETag for the data.
    /// Used to invalidate the cache.
    /// </summary>
    string ETag { get; init; }

}

/// <summary>
/// Interface for cached data with a generic type.
/// </summary>
public interface ICachedData<T> : ICachedData where T : class
{
    /// <summary>
    /// The data.
    /// </summary>
    T Data { get; init; }
}