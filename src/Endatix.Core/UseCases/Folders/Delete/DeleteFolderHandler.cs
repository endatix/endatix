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
using Microsoft.Extensions.Logging;

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
    IMediator mediator,
    ILogger<DeleteFolderHandler> logger, ManagedContentScope? scope) : ICommandHandler<DeleteFolderCommand, Result<string>>
{
    /// <summary>Constructs the ordinary operation without account standing enforcement.</summary>
    public DeleteFolderHandler(IRepository<Folder> folderRepository, IRepository<Form> formRepository,
        IRepository<FormTemplate> formTemplateRepository, ITenantContext tenantContext, IUnitOfWork unitOfWork,
        IMediator mediator, ILogger<DeleteFolderHandler> logger)
        : this(folderRepository, formRepository, formTemplateRepository, tenantContext, unitOfWork, mediator, logger, null) { }

    /// <inheritdoc/>
    public async Task<Result<string>> Handle(DeleteFolderCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(tenantContext.TenantId);

        if (scope is { IsEnforced: true } && await RefusalAsync(request.FolderId, cancellationToken) is { } refusal)
        {
            return refusal;
        }
        return await DeleteAsync(request, cancellationToken);
    }

    private async Task<Result<string>?> RefusalAsync(long id, CancellationToken ct)
    {
        var target = await folderRepository.FirstOrDefaultAsync(new FolderEditTarget.ById(id, tenantContext.TenantId), ct);
        return CannotDelete(target?.Immutable) ?? await scope!.RefusalAsync<string>(ct);
    }

    private static Result<string>? CannotDelete(bool? immutable) => immutable switch
    {
        null => Result.NotFound("Folder not found."),
        true => Result<string>.Conflict("Locked folders cannot be deleted."),
        false => null
    };

    private async Task<Result<string>> DeleteAsync(DeleteFolderCommand request, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            return await DeleteInTransactionAsync(request.FolderId, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to delete folder {FolderId}", request.FolderId);
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Error("Failed to delete folder.");
        }
    }

    private async Task<Result<string>> DeleteInTransactionAsync(long id, CancellationToken ct)
    {
        var spec = new FolderSpecifications.FolderByIdSpec(id);
        spec.InTenant(scope);
        var folder = await folderRepository.FirstOrDefaultAsync(spec, ct);
        if (CannotDelete(folder?.Immutable) is { } denied)
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            return denied;
        }
        await ClearAssignmentsAsync(id, ct);
        await ReparentChildrenAsync(id, ct);
        await mediator.Publish(new FolderDeletedEvent(folder!), ct);
        await folderRepository.DeleteAsync(folder!, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await unitOfWork.CommitTransactionAsync(ct);
        return Result.Success(id.ToString());
    }

    private async Task ClearAssignmentsAsync(long id, CancellationToken ct)
    {
        var forms = new FormSpecifications.ByFolderId(id);
        forms.InTenant(scope);
        foreach (var form in await formRepository.ListAsync(forms, ct))
        {
            form.ClearFolder();
        }
        var templates = new FormTemplateSpecifications.ByFolderId(id);
        templates.InTenant(scope);
        foreach (var template in await formTemplateRepository.ListAsync(templates, ct))
        {
            template.ClearFolder();
        }
    }

    private async Task ReparentChildrenAsync(long id, CancellationToken ct)
    {
        var children = new FolderSpecifications.ByParentFolderIdSpec(id);
        children.InTenant(scope);
        foreach (var child in await folderRepository.ListAsync(children, ct))
        {
            child.ParentFolderId = null;
        }
    }
}
