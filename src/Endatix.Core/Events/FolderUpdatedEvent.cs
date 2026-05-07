using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Event dispatched when an existing folder is updated.
/// </summary> 
/// <param name="folder">The folder that was updated.</param>
public sealed class FolderUpdatedEvent(Folder folder) : DomainEventBase
{
    public Folder Folder { get; init; } = folder;
}
