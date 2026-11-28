using System.Data;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data.Repositories;

internal sealed class FormDraftRepository(AppDbContext db, IUniqueConstraintViolationChecker unique)
    : IFormDraftRepository
{
    public async Task<Result<Form>> FindReadableFormAsync(long tenantId, long formId, CancellationToken cancellationToken)
    {
        var form = tenantId > 0 ? await FindFormAsync(tenantId, formId, cancellationToken) : null;
        if (form is null)
        {
            return Result.NotFound("Form not found.");
        }
        if (await IsUpgradePendingAsync(formId, cancellationToken))
        {
            return Result.Unavailable("This form's draft upgrade is pending. Retry after the upgrade finishes.");
        }
        return form;
    }

    public Task<Result<DraftComparison>> ReadComparisonAsync(long tenantId, long formId, CancellationToken cancellationToken) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            // Protect the missing-head, pending-queue and legacy ordinal ranges as well as the
            // rows we found. SQL Server need not have database snapshot isolation enabled.
            var isolation = db.Database.IsNpgsql() ? IsolationLevel.RepeatableRead : IsolationLevel.Serializable;
            await using var transaction = await db.Database.BeginTransactionAsync(isolation, ct);
            var form = await FindReadableFormAsync(tenantId, formId, ct);
            var comparison = form.IsSuccess ? await ReadDocumentsAsync(form.Value, ct) : form.ToErrorResult<DraftComparison>();
            await transaction.CommitAsync(ct);
            return comparison;
        }, cancellationToken);

    private async Task<Result<DraftComparison>> ReadDocumentsAsync(Form form, CancellationToken ct)
    {
        var head = await db.FormDrafts.Where(row => row.TenantId == form.TenantId && row.FormId == form.Id)
            .Select(row => new { row.Version, row.JsonData, row.ContentHash }).SingleOrDefaultAsync(ct);
        var number = await ActiveNumberAsync(form, ct);
        var published = form.ActiveDefinition?.JsonData;
        return new DraftComparison(head?.Version ?? 0, number, published, head?.JsonData) { DraftHash = head?.ContentHash };
    }

    public Task<Form?> FindFormAsync(long tenantId, long formId, CancellationToken cancellationToken) =>
        db.Forms.AsNoTracking().Include(form => form.ActiveDefinition)
            .SingleOrDefaultAsync(form => form.TenantId == tenantId && form.Id == formId, cancellationToken);

    public Task<FormDraft?> FindHeadAsync(long formId, CancellationToken cancellationToken) =>
        db.FormDrafts.SingleOrDefaultAsync(head => head.FormId == formId, cancellationToken);

    public Task<bool> IsUpgradePendingAsync(long formId, CancellationToken cancellationToken) =>
        db.Set<LegacyDraftUpgrade>().AnyAsync(row => row.FormId == formId, cancellationToken);

    public async Task<int?> ActiveNumberAsync(Form form, CancellationToken cancellationToken)
    {
        if (form.ActiveDefinition is not { } active)
        {
            return null;
        }

        if (active.Number is long number)
        {
            return checked((int)number);
        }

        var definitions = db.FormDefinitions.Where(row => row.FormId == form.Id);
        var lastNumber = await definitions.MaxAsync(row => row.Number, cancellationToken) ?? 0;
        var offset = await definitions.CountAsync(row => row.Number == null && (!row.IsDraft || row.Id == active.Id || row.Submissions.Any())
            && (row.CreatedAt < active.CreatedAt || (row.CreatedAt == active.CreatedAt && row.Id <= active.Id)), cancellationToken);
        return checked((int)lastNumber + offset);
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
