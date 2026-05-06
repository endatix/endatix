using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using TenantSettingsEntity = Endatix.Core.Entities.TenantSettings;

namespace Endatix.Core.UseCases.Folders;

/// <summary>
/// Validates optional folder assignment against tenant policy and folder existence.
/// </summary>
public sealed class FolderAssignmentPolicy(
    IRepository<TenantSettingsEntity> tenantSettingsRepository,
    IRepository<Folder> folderRepository,
    ITenantContext tenantContext)
{
    private const string IMMUTABLE_FOLDER_MOVE_ERROR = "Items in immutable folders cannot be moved or cleared.";

    /// <summary>
    /// Ensures the folder assignment is valid for the current tenant.
    /// </summary>
    /// <param name="folderId">The ID of the folder to assign.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result indicating whether the folder assignment is valid.</returns>
    public async Task<Result> EnsureFolderAssignmentValidAsync(long? folderId, CancellationToken cancellationToken)
    {
        return await EnsureFolderMoveValidAsync(
            currentFolderId: null,
            requestedFolderId: folderId,
            cancellationToken);
    }

    public async Task<Result> EnsureFolderMoveValidAsync(
        long? currentFolderId,
        long? requestedFolderId,
        CancellationToken cancellationToken)
    {
        // No-op moves are allowed only for existing folder assignments.
        // For create flows (null -> null), tenant "RequireFolderAssignment" must still be enforced.
        if (currentFolderId.HasValue &&
            currentFolderId.Value > 0 &&
            currentFolderId == requestedFolderId)
        {
            return Result.Success();
        }

        if (currentFolderId.HasValue && currentFolderId.Value > 0)
        {
            var currentFolder = await folderRepository.FirstOrDefaultAsync(
                new FolderSpecifications.FolderByIdSpec(currentFolderId.Value),
                cancellationToken);

            if (currentFolder?.Immutable == true)
            {
                return Result.Conflict(IMMUTABLE_FOLDER_MOVE_ERROR);
            }
        }

        var tenantSettings = await tenantSettingsRepository.FirstOrDefaultAsync(
            new TenantSettingsByTenantIdSpec(tenantContext.TenantId),
            cancellationToken);

        var require = tenantSettings?.RequireFolderAssignment ?? false;
        if (require && (!requestedFolderId.HasValue || requestedFolderId.Value <= 0))
        {
            return Result.Error("You must assign a folder.");
        }

        if (!requestedFolderId.HasValue || requestedFolderId.Value <= 0)
        {
            return Result.Success();
        }

        var requestedFolder = await folderRepository.FirstOrDefaultAsync(
            new FolderSpecifications.FolderByIdSpec(requestedFolderId.Value),
            cancellationToken);

        if (requestedFolder is null)
        {
            return Result.Error("Folder was not found.");
        }

        if (!requestedFolder.IsActive)
        {
            return Result.Error("Folder is not active.");
        }

        return Result.Success();
    }

    /// <summary>
    /// Validates a folder move and applies the aggregate mutation in one place.
    /// </summary>
    public async Task<Result> EnsureAndApplyFolderMoveAsync(
        long? currentFolderId,
        long? requestedFolderId,
        Func<long?, bool> applyMove,
        string cannotMoveMessage,
        CancellationToken cancellationToken)
    {
        var validation = await EnsureFolderMoveValidAsync(currentFolderId, requestedFolderId, cancellationToken);
        if (!validation.IsOk())
        {
            return validation;
        }

        if (!applyMove(requestedFolderId))
        {
            return Result.Error(cannotMoveMessage);
        }

        return Result.Success();
    }
}

