using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Event dispatched when a new folder is created.
/// </summary>
public sealed class FolderCreatedEvent(Folder folder) : DomainEventBase
{
    /// <summary>
    /// The folder that was created.
    /// </summary>
    public Folder Folder { get; init; } = folder;
}
