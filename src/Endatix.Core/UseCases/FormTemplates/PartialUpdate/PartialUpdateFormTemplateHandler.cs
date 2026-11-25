using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Folders;

namespace Endatix.Core.UseCases.FormTemplates.PartialUpdate;

/// <summary>
/// Handler for partially updating a form template.
/// </summary>
public class PartialUpdateFormTemplateHandler(IRepository<FormTemplate> repository,
    FolderAssignmentPolicy folderAssignmentPolicy, ManagedContentScope? scope)
    : ICommandHandler<PartialUpdateFormTemplateCommand, Result<FormTemplate>>
{
    /// <summary>Constructs the ordinary operation without account standing enforcement.</summary>
    public PartialUpdateFormTemplateHandler(IRepository<FormTemplate> repository,
        FolderAssignmentPolicy folderAssignmentPolicy)
        : this(repository, folderAssignmentPolicy, null) { }

    /// <inheritdoc/>
    public async Task<Result<FormTemplate>> Handle(PartialUpdateFormTemplateCommand request, CancellationToken cancellationToken)
    {
        if (scope is { IsEnforced: true } && await new TemplateMoveAdmission(repository, folderAssignmentPolicy, scope).RefusalAsync(request, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var formTemplate = await repository.FindAsync(request.FormTemplateId, scope, cancellationToken);
        if (formTemplate == null)
        {
            return Result.NotFound("Form template not found.");
        }

        formTemplate.Name = request.Name ?? formTemplate.Name;
        formTemplate.Description = request.Description ?? formTemplate.Description;
        formTemplate.JsonData = request.JsonData ?? formTemplate.JsonData;

        var move = await ApplyMoveAsync(formTemplate, request, cancellationToken);
        if (!move.IsOk())
        {
            return move.ToErrorResult<FormTemplate>();
        }
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
}
