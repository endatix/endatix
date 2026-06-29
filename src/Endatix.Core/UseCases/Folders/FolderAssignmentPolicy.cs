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
    private const string IMMUTABLE_FOLDER_MOVE_ERROR = "Items in locked folders cannot be moved or cleared.";
    private const string NO_OP_FOLDER_MOVE_ERROR = "Folder assignment is unchanged.";

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

        if (IsStatusQuoFolderMove(currentFolderId, requestedFolderId))
        {
            return Result.Invalid(CreateStatusQuoFolderMoveValidationError());
        }

        if (!applyMove(requestedFolderId))
        {
            return Result.Error(cannotMoveMessage);
        }

        return Result.Success();
    }

    /// <summary>
    /// True when current and requested are the same positive folder id (status quo folder move).
    /// Excludes null/null — full replace may omit folder id without intending a PATCH no-op.
    /// </summary>
    private static bool IsStatusQuoFolderMove(long? currentFolderId, long? requestedFolderId) =>
        currentFolderId is long currentId and > 0 &&
        requestedFolderId is long requestedId and > 0 &&
        currentId == requestedId;

    private static ValidationError CreateStatusQuoFolderMoveValidationError() => new(
                identifier: "folderId",
                errorMessage: NO_OP_FOLDER_MOVE_ERROR,
                errorCode: "FOLDER_ASSIGNMENT_UNCHANGED",
                severity: ValidationSeverity.Error);
}

