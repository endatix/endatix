using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Folders.Update;

/// <summary>
/// Command for updating a folder.
/// </summary>      
public sealed record UpdateFolderCommand : ICommand<Result<Folder>>
{
    /// <summary>
    /// The ID of the folder to update.
    /// </summary>
    public long FolderId { get; }

    /// <summary>
    /// The name of the folder to update.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The slug of the folder to update.
    /// </summary>
    public string? Slug { get; init; }

    /// <summary>
    /// The description of the folder to update.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The metadata of the folder to update.
    /// </summary>
    public string? Metadata { get; init; }

    /// <summary>
    /// Whether the folder is active.
    /// </summary>
    public bool? IsActive { get; init; }

    /// <summary>
    /// Whether the folder is immutable.
    /// </summary>
    public bool? Immutable { get; init; }

    public UpdateFolderCommand(long folderId)
    {
        Guard.Against.NegativeOrZero(folderId);
        FolderId = folderId;
    }
}
