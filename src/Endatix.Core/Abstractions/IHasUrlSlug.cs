namespace Endatix.Core.Abstractions;

/// <summary>
/// Marks an entity that exposes a tenant-scoped URL path segment (kebab-case; see <see cref="Common.UrlSlugNormalizer"/>).
/// <para>
/// This is the <em>readable</em> kind of slug: derived from a display name, meaningful to a human,
/// unique per tenant, re-derived when the name changes, and checked against
/// <see cref="Common.UrlSlugNormalizer.ReservedSlugs"/>. Revealing the name is the point.
/// </para>
/// <para>
/// It is not the same concept as a short URL identifier (<see cref="Common.ShortUrl"/>,
/// <see cref="ShortUrlKind"/>) such as <c>Tenant.ShortUrl</c>: those optimise for length and global
/// uniqueness rather than readability - random, fixed-length, immutable, and deliberately
/// <em>not</em> reserved-word checked, because their job is to keep the name out of the URL. Do not
/// implement this interface on an entity whose segment is a short URL identifier, and do not run
/// such values through <see cref="Common.UrlSlugNormalizer"/> - normalization would corrupt them
/// and the reserved-word list does not apply.
/// </para>
/// </summary>
public interface IHasUrlSlug
{
    /// <summary>
    /// Stable, URL-safe segment for routing and lookups (e.g. folder hub paths).
    /// </summary>
    string UrlSlug { get; set; }
}
