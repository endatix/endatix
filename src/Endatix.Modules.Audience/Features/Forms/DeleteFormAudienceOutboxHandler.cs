using System.Text.Json;
using Endatix.Core.Events;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Audience.Persistence;
using Endatix.Outbox.Engine;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Modules.Audience.Features.Forms;

/// <summary>
/// Soft-deletes a deleted form's audience: its value cells, memberships and properties. Tenant
/// members stay, as when a person is removed from one form. Without this, the people on a deleted
/// form would keep the match key locked with no route left to remove them.
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
        await SoftDeleteFormAudienceAsync(form.TenantId, form.FormId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task SoftDeleteFormAudienceAsync(long tenantId, long formId, CancellationToken cancellationToken)
    {
        await db.PropertyValues
            .Where(value => value.TenantId == tenantId
                && db.Memberships
                    .Where(membership => membership.TenantId == tenantId && membership.FormId == formId)
                    .Select(membership => membership.Id)
                    .Contains(value.MembershipId))
            .SoftDeleteAllAsync(cancellationToken);
        await db.Memberships
            .Where(membership => membership.TenantId == tenantId && membership.FormId == formId)
            .SoftDeleteAllAsync(cancellationToken);
        await db.Properties
            .Where(property => property.TenantId == tenantId && property.FormId == formId)
            .SoftDeleteAllAsync(cancellationToken);
    }

    internal sealed record DeletedForm(long TenantId, long FormId);
}
