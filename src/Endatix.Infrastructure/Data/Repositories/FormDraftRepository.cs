using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data.Repositories;

internal sealed class FormDraftRepository(AppDbContext db, IUniqueConstraintViolationChecker unique)
    : IFormDraftRepository
{
    public Task<Form?> FindFormAsync(long tenantId, long formId, CancellationToken cancellationToken) =>
        db.Forms.AsNoTracking().Include(form => form.ActiveDefinition)
            .SingleOrDefaultAsync(form => form.TenantId == tenantId && form.Id == formId, cancellationToken);

    public Task<FormDraft?> FindHeadAsync(long formId, CancellationToken cancellationToken) =>
        db.FormDrafts.SingleOrDefaultAsync(head => head.FormId == formId, cancellationToken);

    public async Task<int?> ActiveNumberAsync(Form form, CancellationToken cancellationToken)
    {
        if (form.ActiveDefinition is not { } active)
        {
            return null;
        }

        return await db.FormDefinitions.CountAsync(row => row.FormId == form.Id && (!row.IsDraft || row.Id == active.Id || row.Submissions.Any())
            && (row.CreatedAt < active.CreatedAt || (row.CreatedAt == active.CreatedAt && row.Id <= active.Id)), cancellationToken);
    }

    public Task<Actor?> LastActorAsync(long formId, long version, CancellationToken cancellationToken) =>
        db.FormDraftOperations.AsNoTracking()
            .Where(row => row.FormId == formId && row.Version == version)
            .Select(row => row.Actor)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<long> LatestSnapshotVersionAsync(long formId, long throughVersion, CancellationToken cancellationToken) =>
        await db.FormDraftOperations.Where(row => row.FormId == formId && row.Version <= throughVersion && row.Kind == FormDraftOperationKinds.SNAPSHOT)
            .MaxAsync(row => (long?)row.Version, cancellationToken) ?? 0;

    public async Task<bool> SaveOrConcedeAsync(FormDraft head, IReadOnlyList<FormDraftOperation> rows, CancellationToken cancellationToken)
    {
        if (db.Entry(head).State == EntityState.Detached)
        {
            db.FormDrafts.Add(head);
        }

        db.FormDraftOperations.AddRange(rows);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsLostRace(exception))
        {
            Detach(head, rows);
            return false;
        }
    }

    private void Detach(FormDraft head, IReadOnlyList<FormDraftOperation> rows)
    {
        foreach (var row in rows)
        {
            db.Entry(row).State = EntityState.Detached;
        }
        db.Entry(head).State = EntityState.Detached;
    }

    private bool IsLostRace(DbUpdateException exception) =>
        exception is DbUpdateConcurrencyException || unique.AnalyzeUniqueConstraint(exception).IsUniqueConstraintViolation;
}
