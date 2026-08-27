namespace Endatix.Core.Abstractions;

/// <summary>
/// Kinds of opaque public ids. Length is per kind; uniqueness is per aggregate unique index.
/// </summary>
public enum PublicIdKind
{
    /// <summary>
    /// Tenant URL token stored on <c>Tenant.Slug</c> (8 characters).
    /// </summary>
    Tenant = 0
}

/// <summary>
/// Generates cryptographically random public ids (nanoid / YouTube alphabet).
/// </summary>
public interface IPublicIdGenerator
{
    /// <summary>
    /// Creates a new public id for <paramref name="kind"/>. Callers retry on unique-index collisions.
    /// </summary>
    string Create(PublicIdKind kind);
}
