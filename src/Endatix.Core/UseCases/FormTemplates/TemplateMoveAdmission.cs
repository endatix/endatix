using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Folders;
using Endatix.Core.UseCases.FormTemplates.PartialUpdate;

namespace Endatix.Core.UseCases.FormTemplates;

internal sealed class TemplateMoveAdmission(IRepository<FormTemplate> templates, FolderAssignmentPolicy folders, ManagedContentScope scope)
{
    internal async Task<Result<FormTemplate>?> RefusalAsync(PartialUpdateFormTemplateCommand request, CancellationToken ct)
    {
        var target = scope.ById<FormTemplate>(request.FormTemplateId);
        var current = await templates.FirstOrDefaultAsync(target.WithProjectionOf(new FolderAssignment()), ct);
        if (current is null)
        {
            return Result.NotFound("Form template not found.");
        }
        if (request.ClearFolderId || request.FolderId.HasValue)
        {
            var move = await folders.CheckMoveAsync(current.FolderId, request.ClearFolderId ? null : request.FolderId, ct);
            if (!move.IsOk())
            {
                return move.ToErrorResult<FormTemplate>();
            }
        }
        return await scope.RefusalAsync<FormTemplate>(ct);
    }

    private sealed record Assignment(long? FolderId);
    private sealed class FolderAssignment : Specification<FormTemplate, Assignment>
    {
        internal FolderAssignment() => Query.Select(template => new Assignment(template.FolderId));
    }
}
