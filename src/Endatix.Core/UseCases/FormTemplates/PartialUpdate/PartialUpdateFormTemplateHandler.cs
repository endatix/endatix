using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Folders;

namespace Endatix.Core.UseCases.FormTemplates.PartialUpdate;

/// <summary>
/// Handler for partially updating a form template.
/// </summary>
public class PartialUpdateFormTemplateHandler(
    IRepository<FormTemplate> repository,
    FolderAssignmentPolicy folderAssignmentPolicy)
    : ICommandHandler<PartialUpdateFormTemplateCommand, Result<FormTemplate>>
{
    /// <inheritdoc/>
    public async Task<Result<FormTemplate>> Handle(PartialUpdateFormTemplateCommand request, CancellationToken cancellationToken)
    {
        var formTemplate = await repository.GetByIdAsync(request.FormTemplateId, cancellationToken);
        if (formTemplate == null)
        {
            return Result.NotFound("Form template not found.");
        }

        formTemplate.Name = request.Name ?? formTemplate.Name;
        formTemplate.Description = request.Description ?? formTemplate.Description;
        formTemplate.JsonData = request.JsonData ?? formTemplate.JsonData;

        if (request.ClearFolderId)
        {
            var clearCheck = await folderAssignmentPolicy.EnsureAndApplyFolderMoveAsync(
                formTemplate.FolderId,
                null,
                _ => formTemplate.ClearFolder(),
                "Form template cannot be moved to the requested folder.",
                cancellationToken);
            if (!clearCheck.IsOk())
            {
                return clearCheck.ToErrorResult<FormTemplate>();
            }
        }
        else if (request.FolderId.HasValue)
        {
            var folderCheck = await folderAssignmentPolicy.EnsureAndApplyFolderMoveAsync(
                formTemplate.FolderId,
                request.FolderId,
                targetFolderId => formTemplate.MoveToFolder(targetFolderId),
                "Form template cannot be moved to the requested folder.",
                cancellationToken);
            if (!folderCheck.IsOk())
            {
                return folderCheck.ToErrorResult<FormTemplate>();
            }
        }

        await repository.UpdateAsync(formTemplate, cancellationToken);
        return Result.Success(formTemplate);
    }
}
