namespace Endatix.Core.Abstractions;

/// <summary>
/// Interface for entities that support ownership checking
/// </summary>
public interface IOwnedEntity
{
    /// <summary>
    /// Gets the ID of the user who owns this entity
    /// </summary>
    string? OwnerId { get; }
}