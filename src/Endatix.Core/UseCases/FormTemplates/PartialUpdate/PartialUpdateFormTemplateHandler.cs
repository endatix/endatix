using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Folders;

namespace Endatix.Core.UseCases.FormTemplates.PartialUpdate;

/// <summary>
/// Handler for partially updating a form template.
/// </summary>
[DirectCallOnly]
public class PartialUpdateFormTemplateHandler(IRepository<FormTemplate> repository,
    FolderAssignmentPolicy folderAssignmentPolicy) : ICommandHandler<PartialUpdateFormTemplateCommand, Result<FormTemplate>>
{
    private ContentQueryScope Selection { get; init; } = ContentQueryScope.Repository;

    /// <inheritdoc/>
    public async Task<Result<FormTemplate>> Handle(PartialUpdateFormTemplateCommand request, CancellationToken cancellationToken)
    {
        var formTemplate = await Selection.FindAsync(repository, request.FormTemplateId, cancellationToken);
        if (formTemplate == null)
        {
            return Result.NotFound("Form template not found.");
        }

        var move = await ApplyMoveAsync(formTemplate, request, cancellationToken);
        if (!move.IsOk())
        {
            return move.ToErrorResult<FormTemplate>();
        }
        formTemplate.Name = request.Name ?? formTemplate.Name;
        formTemplate.Description = request.Description ?? formTemplate.Description;
        formTemplate.JsonData = request.JsonData ?? formTemplate.JsonData;

        await repository.UpdateAsync(formTemplate, cancellationToken);
        return Result.Success(formTemplate);
    }

    private Task<Result> ApplyMoveAsync(FormTemplate template, PartialUpdateFormTemplateCommand request, CancellationToken ct)
    {
        if (!request.ClearFolderId && !request.FolderId.HasValue)
        {
            return Task.FromResult(Result.Success());
        }
        var destination = request.ClearFolderId ? null : request.FolderId;
        return folderAssignmentPolicy.EnsureAndApplyFolderMoveAsync(template.FolderId, destination,
            template.MoveToFolder, "Form template cannot be moved to the requested folder.", ct);
    }

    internal sealed class Registered(IRepository<FormTemplate> repository, FolderAssignmentPolicy folderAssignmentPolicy, ContentQueryScope scope)
        : RegisteredContentHandler<PartialUpdateFormTemplateCommand, Result<FormTemplate>>(
            new PartialUpdateFormTemplateHandler(repository, folderAssignmentPolicy) { Selection = scope });
}
