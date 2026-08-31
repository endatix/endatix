namespace Endatix.Core.Abstractions;

/// <summary>
/// Shapes of short URL identifiers. The kind fixes the length; uniqueness is enforced by each
/// aggregate's own unique index, so the same kind can be reused across entities.
/// </summary>
public enum ShortUrlKind
{
    /// <summary>
    /// 8-character token over a 36-symbol alphabet. Used for <c>Tenant.ShortUrl</c>.
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
