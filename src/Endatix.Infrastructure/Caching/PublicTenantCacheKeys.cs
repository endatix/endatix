using Endatix.Core.Common;

namespace Endatix.Infrastructure.Caching;

/// <summary>
/// HybridCache keys/tags for unauthenticated tenant discovery.
/// </summary>
public static class PublicTenantCacheKeys
{
    public const string Tag = "tenant:public";

    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(3);

    public static string Entry(string normalizedSlug) => $"{Tag}:{normalizedSlug}";

    public static string[] TagsFor(string normalizedSlug) => [Tag, Entry(normalizedSlug)];

    /// <summary>
    /// Normalized public id, or null when the inbound value is not a valid short URL.
    /// </summary>
    public static string? TryNormalized(string? slug)
    {
        var normalized = ShortUrl.Normalize(slug);
        if (normalized is null || !ShortUrl.IsValid(normalized))
        {
            return null;
        }

        return normalized;
    }
}
