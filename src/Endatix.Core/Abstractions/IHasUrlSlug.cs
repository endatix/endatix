namespace Endatix.Core.Abstractions;

/// <summary>
/// Marks an entity that exposes a tenant-scoped URL path segment (kebab-case; see <see cref="Common.UrlSlugNormalizer"/>).
/// </summary>
public interface IHasUrlSlug
{
    /// <summary>
    /// Stable, URL-safe segment for routing and lookups (e.g. folder hub paths).
    /// </summary>
    string UrlSlug { get; set; }
}
