namespace Endatix.Core.Abstractions;

/// <summary>
/// Shapes of opaque public ids. The kind fixes the length; uniqueness is enforced by each
/// aggregate's own unique index, so the same kind can be reused across entities.
/// </summary>
public enum PublicIdKind
{
    /// <summary>
    /// Short 8-character alphanumeric token for brief public URLs (62^8 ≈ 2.2 * 10^14 combinations).
    /// Sized for aggregates in the 10K-20K range, where collision risk stays negligible and a
    /// create can simply redraw. Used today for <c>Tenant.Slug</c>; reusable for any entity that
    /// needs a compact, non-guessable URL segment.
    /// </summary>
    ShortSlug = 0
}

/// <summary>
/// Generates cryptographically random public ids (alphanumeric alphabet).
/// </summary>
public interface IPublicIdGenerator
{
    /// <summary>
    /// Creates a new public id for <paramref name="kind"/>. Callers retry on unique-index collisions.
    /// </summary>
    string Create(PublicIdKind kind);
}
