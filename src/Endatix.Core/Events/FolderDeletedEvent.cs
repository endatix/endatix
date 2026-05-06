using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Event dispatched when a folder is deleted.
/// </summary>
public sealed class FolderDeletedEvent(Folder folder) : DomainEventBase
{
    /// <summary>
    /// The folder that was deleted.
    /// </summary>
    public Folder Folder { get; init; } = folder;
}
