using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;

namespace Endatix.Core.UseCases.Folders.Delete;

/// <summary>
/// Deletes a folder: clears <see cref="Form"/> / <see cref="FormTemplate"/> folder assignments,
/// reparents child folders to root, then removes the folder row.
/// </summary>
public sealed class DeleteFolderHandler(
    IRepository<Folder> folderRepository,
    IRepository<Form> formRepository,
    IRepository<FormTemplate> formTemplateRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IMediator mediator) : ICommandHandler<DeleteFolderCommand, Result<string>>
{
    /// <inheritdoc/>
    public async Task<Result<string>> Handle(DeleteFolderCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(tenantContext.TenantId);

        await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var folder = await folderRepository.FirstOrDefaultAsync(
                new FolderSpecifications.FolderByIdSpec(request.FolderId),
                cancellationToken);

            if (folder is null)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.NotFound("Folder not found.");
            }

            if (folder.Immutable)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result<string>.Conflict("Locked folders cannot be deleted.");
            }

            var folderId = folder.Id;

            var forms = await formRepository.ListAsync(
                new FormSpecifications.ByFolderId(request.FolderId),
                cancellationToken);

            foreach (var form in forms)
            {
                form.ClearFolder();
            }

            var templates = await formTemplateRepository.ListAsync(
                new FormTemplateSpecifications.ByFolderId(request.FolderId),
                cancellationToken);

            foreach (var template in templates)
            {
                template.ClearFolder();
            }

            var childFolders = await folderRepository.ListAsync(
                new FolderSpecifications.ByParentFolderIdSpec(request.FolderId),
                cancellationToken);

            foreach (var child in childFolders)
            {
                child.ParentFolderId = null;
            }

            await mediator.Publish(new FolderDeletedEvent(folder), cancellationToken);

            await folderRepository.DeleteAsync(folder, cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result.Success(folderId.ToString());
        }
        catch (Exception exception)
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Error($"Error deleting folder: {exception.Message}");
        }
    }
}
