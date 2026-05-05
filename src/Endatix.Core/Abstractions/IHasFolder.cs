namespace Endatix.Core.Abstractions;

/// <summary>
/// Marks an aggregate that can be assigned to a tenant folder.
/// </summary>
public interface IHasFolder
{
    /// <summary>
    /// Nullable folder identifier when item is organized under a folder.
    /// </summary>
    long? FolderId { get; }

    /// <summary>
    /// Domain guard that decides whether this aggregate can move to the requested folder.
    /// </summary>
    bool CanMoveToFolder(long? folderId);

    /// <summary>
    /// Assigns this aggregate to a folder (or clears assignment with null).
    /// Returns false when domain rules reject the move.
    /// </summary>
    bool MoveToFolder(long? folderId);

    /// <summary>
    /// Clears any folder assignment.
    /// </summary>
    bool ClearFolder();
}
