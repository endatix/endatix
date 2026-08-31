namespace Endatix.Core.Abstractions;

/// <summary>
/// Marks an entity that exposes a tenant-scoped readable URL path segment
/// (kebab-case; see <see cref="Common.UrlSlugNormalizer"/>). Name-derived, unique per tenant,
/// reserved-word checked. Not the same as <see cref="Common.ShortUrl"/> (opaque, globally unique).
/// </summary>
public interface IHasUrlSlug
{
    /// <summary>
    /// Stable, URL-safe segment for routing and lookups (e.g. folder hub paths).
    /// </summary>
    string UrlSlug { get; set; }
}
