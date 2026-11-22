namespace Endatix.Core.Versioning;

/// <summary>
/// One published version of a content item: an immutable row, numbered 1..n per item, recording what was published,
/// when, by whom and how. Tenant branding uses it now; themes and form definitions are meant to take the same fields.
/// </summary>
/// <remarks>
/// Versions are only appended. A rollback restores an older version as a new one (<see cref="ContentVersionSources.RESTORE"/>),
/// so history never rewinds and two rollbacks in a row toggle between the last two versions.
/// </remarks>
internal interface IContentVersion
{
    /// <summary>The version's number, 1 for the first, unique per item.</summary>
    long Number { get; }

    /// <summary>The published content, as canonical JSON.</summary>
    string Json { get; }

    /// <summary>The SHA-256 of the canonical JSON (<see cref="CanonicalContentHash"/>): equal content, equal hash.</summary>
    string ContentHash { get; }

    /// <summary>When the version was published.</summary>
    DateTime CreatedAt { get; }

    /// <summary>The user who published it, when known.</summary>
    long? CreatedBy { get; }

    /// <summary>How it was made: one of <see cref="ContentVersionSources"/>.</summary>
    string Source { get; }

    /// <summary>The number of the version it copies, for a restore (or a reset made by restoring one); otherwise null.</summary>
    long? RestoredFromNumber { get; }
}
