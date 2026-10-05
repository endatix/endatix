using System.Text.Json;
using Endatix.Core.Events;
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
internal sealed class DeleteFormAudienceOutboxHandler(IAudienceDbContext db) : IOutboxIntegrationEventHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<string> EventTypes { get; } = [FormDeletedEvent.EventTypeName];

    /// <inheritdoc />
    public async Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken)
    {
        using JsonDocument document = JsonDocument.Parse(message.Payload);
        long tenantId = message.GetRequiredTenantId(document.RootElement);
        long formId = message.GetRequiredIdProp(document.RootElement, "formId");

        await using IDbContextTransaction transaction =
            await ((DbContext)db).Database.BeginTransactionAsync(cancellationToken);
        await SoftDeleteFormAudienceAsync(tenantId, formId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task SoftDeleteFormAudienceAsync(long tenantId, long formId, CancellationToken cancellationToken)
    {
        await db.PropertyValues
            .Where(value => value.TenantId == tenantId
                && db.Memberships.Any(membership => membership.Id == value.MembershipId && membership.FormId == formId))
            .SoftDeleteAllAsync(cancellationToken);
        await db.Memberships
            .Where(membership => membership.TenantId == tenantId && membership.FormId == formId)
            .SoftDeleteAllAsync(cancellationToken);
        await db.Properties
            .Where(property => property.TenantId == tenantId && property.FormId == formId)
            .SoftDeleteAllAsync(cancellationToken);
    }
}
