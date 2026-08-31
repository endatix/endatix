namespace Endatix.Core.Abstractions;

/// <summary>
/// Shapes of short URL identifiers. The kind fixes the length; uniqueness is enforced by each
/// aggregate's own unique index, so the same kind can be reused across entities.
/// </summary>
public enum ShortUrlKind
{
    /// <summary>
    /// 8-character token over a 36-symbol alphabet (~2.8 * 10^12 combinations). Sized for
    /// aggregates in the 10K-20K range, where collision risk stays negligible and a create can
    /// simply redraw. Used today for <c>Tenant.ShortUrl</c>; reusable for any entity that needs a
    /// compact, unguessable URL segment.
    /// </summary>
    Standard = 0
}

/// <summary>
/// Generates cryptographically random short URL identifiers (lowercase alphanumeric alphabet).
/// </summary>
public interface IShortUrlGenerator
{
    /// <summary>
    /// Creates a new short URL identifier for <paramref name="kind"/>.
    /// Callers retry on unique-index collisions.
    /// </summary>
    string Create(ShortUrlKind kind);
}
