using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using static Endatix.Core.Specifications.FolderSpecifications;
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
    private long TenantId => tenantContext.TenantId;

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
        if (TenantId <= 0)
        {
            return Result.Forbidden();
        }
        // No-op moves are allowed only for existing folder assignments.
        // For create flows (null -> null), tenant "RequireFolderAssignment" must still be enforced.
        if (currentFolderId.HasValue &&
            currentFolderId.Value > 0 &&
            currentFolderId == requestedFolderId)
        {
            return Result.Success();
        }

        if (currentFolderId is > 0 && await folderRepository.AnyAsync(new LockedFolder(currentFolderId.Value, TenantId), cancellationToken))
        {
            return Result.Conflict(IMMUTABLE_FOLDER_MOVE_ERROR);
        }

        var require = await tenantSettingsRepository.AnyAsync(new RequiresFolder(TenantId), cancellationToken);
        if (require && (!requestedFolderId.HasValue || requestedFolderId.Value <= 0))
        {
            return Result.Error("You must assign a folder.");
        }

        if (!requestedFolderId.HasValue || requestedFolderId.Value <= 0)
        {
            return Result.Success();
        }

        var active = await folderRepository.FirstOrDefaultAsync(new FolderActive(requestedFolderId.Value, TenantId), cancellationToken);

        if (active is null)
        {
            return Result.Error("Folder was not found.");
        }

        if (!active.Value)
        {
            return Result.Error("Folder is not active.");
        }

        return Result.Success();
    }

    private sealed class RequiresFolder : Specification<TenantSettingsEntity>
    {
        internal RequiresFolder(long tenantId) => Query.Where(settings => settings.TenantId == tenantId && settings.RequireFolderAssignment);
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
        var validation = await CheckMoveAsync(currentFolderId, requestedFolderId, cancellationToken);
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

    internal async Task<Result> CheckMoveAsync(long? currentFolderId, long? requestedFolderId, CancellationToken ct)
    {
        var validation = await EnsureFolderMoveValidAsync(currentFolderId, requestedFolderId, ct);
        return validation.IsOk() && IsStatusQuoFolderMove(currentFolderId, requestedFolderId)
            ? Result.Invalid(CreateStatusQuoFolderMoveValidationError())
            : validation;
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
