using Ardalis.Specification;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Folders;
using Endatix.Core.UseCases.Forms.PartialUpdate;
using Endatix.Core.UseCases.Forms.Update;

namespace Endatix.Core.UseCases.Forms;

internal sealed class ManagedFormEditAccess(IRepository<Form> forms, FolderAssignmentPolicy folders, AccountStanding standing,
    IRepository<Submission> submissions)
{
    internal async Task<Result<Form>?> RefusalAsync(UpdateFormCommand request, CancellationToken ct)
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
        var move = await folders.CheckMoveAsync(target.FolderId, request.FolderId, ct);
        return await DecideAsync(move, target.Change(request.LimitOnePerUser), ct);
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

        var assignment = await CheckAssignmentsAsync(target, request, themes, ct);
        return await DecideAsync(assignment, target.Change(request.LimitOnePerUser, request.IsPublic), ct);
    }

    private async Task<Result> CheckAssignmentsAsync(Assignments current, PartialUpdateFormCommand request, IRepository<Theme> themes, CancellationToken ct)
    {
        if (request.ThemeId is long id && id > 0 && id != current.ThemeId && !await themes.AnyAsync(new ThemeById(id, folders.TenantId), ct))
        {
            return Result.NotFound("Form Theme not found.");
        }
        if (!request.ClearFolderId && !request.FolderId.HasValue)
        {
            return Result.Success();
        }
        return await folders.CheckMoveAsync(current.FolderId, request.ClearFolderId ? null : request.FolderId, ct);
    }

    private Task<Assignments?> FindAsync(long id, CancellationToken ct) => folders.TenantId <= 0
        ? Task.FromResult<Assignments?>(null)
        : forms.FirstOrDefaultAsync(new FormSpecifications.ByIdForTenant(id, folders.TenantId).WithProjectionOf(new EditState()), ct);

    private async Task<Result<Form>?> DecideAsync(Result assignment, SingleSubmissionGateChange change, CancellationToken ct)
    {
        // An invalid assignment already rules out standing. The ordinary write reports its gate conflict first.
        if (!assignment.IsOk())
        {
            return await change.ConflictAsync(submissions, ct) ?? assignment.ToErrorResult<Form>();
        }
        return await standing.RefusalAsync<Form>(ct);
    }

    private sealed record Assignments(long Id, long TenantId, long? FolderId, long? ThemeId, bool Limited, bool Public)
    {
        internal SingleSubmissionGateChange Change(bool? limit, bool? isPublic = null) =>
            new(Id, TenantId, Limited, limit ?? Limited, isPublic ?? Public);
    }

    private sealed class EditState : Specification<Form, Assignments>
    {
        internal EditState() => Query.Select(f => new Assignments(f.Id, f.TenantId, f.FolderId, f.ThemeId, f.LimitOnePerUser, f.IsPublic));
    }

    private sealed class ThemeById : Specification<Theme>
    {
        internal ThemeById(long id, long tenant) => Query.Where(theme => theme.Id == id && theme.TenantId == tenant);
    }
}
