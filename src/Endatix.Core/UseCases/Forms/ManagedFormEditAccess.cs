using Ardalis.Specification;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Folders;
using Endatix.Core.UseCases.Forms.PartialUpdate;
using Endatix.Core.UseCases.Forms.Update;

namespace Endatix.Core.UseCases.Forms;

internal sealed class ManagedFormEditAccess(IRepository<Form> forms, FolderAssignmentPolicy folders, AccountStanding standing)
{
    internal async Task<Result<Form>?> RefusalAsync(UpdateFormCommand request, CancellationToken ct)
    {
        if (!standing.IsEnforced)
        {
            return null;
        }

        var target = await FindAsync(request.FormId, ct);
        return target is null ? Result.NotFound("Form not found.") : await CheckMoveAsync(target, request.FolderId, ct);
    }

    internal async Task<Result<Form>?> RefusalAsync(PartialUpdateFormCommand request, IRepository<Theme> themes, CancellationToken ct)
    {
        if (!standing.IsEnforced)
        {
            return null;
        }

        var target = await FindAsync(request.FormId, ct);
        if (target is null)
        {
            return Result.NotFound("Form not found.");
        }

        if (request.ThemeId is long id && id > 0 && id != target.ThemeId && !await themes.AnyAsync(new ThemeById(id), ct))
        {
            return Result.NotFound("Form Theme not found.");
        }

        return await CheckPatchMoveAsync(target, request, ct);
    }

    private Task<Result<Form>?> CheckPatchMoveAsync(Assignments current, PartialUpdateFormCommand request, CancellationToken ct)
    {
        if (!request.ClearFolderId && !request.FolderId.HasValue)
        {
            return standing.RefusalAsync<Form>(ct);
        }
        return CheckMoveAsync(current, request.ClearFolderId ? null : request.FolderId, ct);
    }

    private Task<Assignments?> FindAsync(long id, CancellationToken ct) =>
        forms.FirstOrDefaultAsync(new AssignmentsById(id), ct);

    private async Task<Result<Form>?> CheckMoveAsync(Assignments current, long? folderId, CancellationToken ct)
    {
        var move = await folders.CheckMoveAsync(current.FolderId, folderId, ct);
        return move.IsOk() ? await standing.RefusalAsync<Form>(ct) : move.ToErrorResult<Form>();
    }

    private sealed record Assignments(long? FolderId, long? ThemeId);

    private sealed class AssignmentsById : SingleResultSpecification<Form, Assignments>
    {
        internal AssignmentsById(long id) =>
            Query.Where(form => form.Id == id).Select(form => new Assignments(form.FolderId, form.ThemeId));
    }

    private sealed class ThemeById : Specification<Theme>
    {
        internal ThemeById(long id) => Query.Where(theme => theme.Id == id);
    }
}
