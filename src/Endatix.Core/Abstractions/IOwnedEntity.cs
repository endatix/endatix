namespace Endatix.Core.Abstractions;

/// <summary>
/// Interface for entities that support ownership checking
/// </summary>
public interface IOwnedEntity
{
    /// <summary>
    /// Checks if the entity is owned by the specified user
    /// </summary>
    /// <param name="userId">The user ID to check ownership for</param>
    /// <returns>True if the user owns this entity, false otherwise</returns>
    bool IsOwnedBy(string userId);
}