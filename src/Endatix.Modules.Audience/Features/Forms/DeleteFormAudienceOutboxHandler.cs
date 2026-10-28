using System.Text.Json;
using Endatix.Core.Events;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Endatix.Outbox.Engine;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Modules.Audience.Features.Forms;

/// <summary>
/// Removes a deleted form's audience (<see cref="FormAudienceCleanup"/>). Without this, the people on a
/// deleted form would keep the match key locked with no route left to remove them.
/// </summary>
internal sealed class DeleteFormAudienceOutboxHandler(IAudienceDbContext db)
    : IOutboxWork<DeleteFormAudienceOutboxHandler>
{
    public static string JobType => "AudienceDeleteFormAudience";

    /// <summary>The event types this handler, and its job, run on.</summary>
    public static IReadOnlyCollection<string> HandledEventTypes { get; } = [FormDeletedEvent.EventTypeName];

    /// <inheritdoc />
    public OutboxWork Read(IOutboxMessage message) =>
        OutboxWork.For(Parse(message), ProcessAsync);

    /// <exception cref="InvalidOperationException">The message lacks its tenant or form id.</exception>
    /// <exception cref="JsonException">The payload is not JSON.</exception>
    public static DeletedForm Parse(IOutboxMessage message)
    {
        using var document = JsonDocument.Parse(message.Payload);
        return new DeletedForm(
            message.GetRequiredTenantId(document.RootElement),
            message.GetRequiredIdProp(document.RootElement, "formId"));
    }

    public async Task ProcessAsync(DeletedForm form, CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction =
            await ((DbContext)db).Database.BeginTransactionAsync(cancellationToken);
        await FormAudienceCleanup.RunAsync(db, form, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    internal sealed record DeletedForm(long TenantId, long FormId);
}

/// <summary>
/// Soft-deletes a deleted form's audience: its value cells, memberships and properties. Tenant members stay, as when
/// a person is removed from one form. An import no phase is running (uploading, or checked) is canceled, so the form
/// keeps no unfinished import; a running phase fails in its own run. Its imports' staged rows and rejections go
/// for good; the import records stay.
/// Safe to run again: the form-deletion handler runs it, and so does an import write that finds the form gone, in
/// case its last batch committed after the handler had run.
/// </summary>
internal static class FormAudienceCleanup
{
    public static async Task RunAsync(
        IAudienceDbContext db,
        DeleteFormAudienceOutboxHandler.DeletedForm form,
        CancellationToken cancellationToken)
    {
        await SoftDeleteAudienceAsync(db, form, cancellationToken);
        await CancelIdleImportsAsync(db, form, cancellationToken);
        await DeleteStagedImportsAsync(db, form, cancellationToken);
    }

    private static async Task CancelIdleImportsAsync(
        IAudienceDbContext db,
        DeleteFormAudienceOutboxHandler.DeletedForm form,
        CancellationToken cancellationToken)
    {
        List<AudienceImport> idle = await db.AudienceImports
            .Where(import => import.TenantId == form.TenantId
                && import.FormId == form.FormId
                && (import.Status == AudienceImportStatusCodes.Uploading
                    || import.Status == AudienceImportStatusCodes.Checked))
            .ToListAsync(cancellationToken);
        foreach (AudienceImport import in idle)
        {
            import.Cancel();
        }

        await SaveUnlessMovedAsync(db, cancellationToken);
    }

    /// <summary>An import the sweep or its run ended meanwhile keeps that end; the other cancels still save.</summary>
    private static async Task SaveUnlessMovedAsync(IAudienceDbContext db, CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException moved)
            {
                Detach(moved.Entries);
            }
        }
    }

    private static void Detach(IEnumerable<EntityEntry> entries)
    {
        foreach (EntityEntry entry in entries)
        {
            entry.State = EntityState.Detached;
        }
    }

    private static async Task SoftDeleteAudienceAsync(
        IAudienceDbContext db,
        DeleteFormAudienceOutboxHandler.DeletedForm form,
        CancellationToken cancellationToken)
    {
        IQueryable<long> memberships = db.Memberships
            .Where(membership => membership.TenantId == form.TenantId && membership.FormId == form.FormId)
            .Select(membership => membership.Id);
        await db.PropertyValues
            .Where(value => value.TenantId == form.TenantId && memberships.Contains(value.MembershipId))
            .SoftDeleteAllAsync(cancellationToken);
        await db.Memberships
            .Where(membership => membership.TenantId == form.TenantId && membership.FormId == form.FormId)
            .SoftDeleteAllAsync(cancellationToken);
        await db.Properties
            .Where(property => property.TenantId == form.TenantId && property.FormId == form.FormId)
            .SoftDeleteAllAsync(cancellationToken);
    }

    private static async Task DeleteStagedImportsAsync(
        IAudienceDbContext db,
        DeleteFormAudienceOutboxHandler.DeletedForm form,
        CancellationToken cancellationToken)
    {
        IQueryable<long> imports = db.AudienceImports
            .Where(import => import.TenantId == form.TenantId && import.FormId == form.FormId)
            .Select(import => import.Id);
        await db.ImportChunks
            .Where(chunk => chunk.TenantId == form.TenantId && imports.Contains(chunk.ImportId))
            .ExecuteDeleteAsync(cancellationToken);
        await db.ImportRejections
            .Where(rejection => rejection.TenantId == form.TenantId && imports.Contains(rejection.ImportId))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
