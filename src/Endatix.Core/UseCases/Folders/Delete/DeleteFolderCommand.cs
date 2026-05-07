using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Folders.Delete;

/// <summary>
/// Deletes a folder and clears folder assignments on contained forms and templates.
/// </summary>
public sealed record DeleteFolderCommand : ICommand<Result<string>>
{
    public long FolderId { get; }

    public DeleteFolderCommand(long folderId)
    {
        Guard.Against.NegativeOrZero(folderId);
        FolderId = folderId;
    }
}
