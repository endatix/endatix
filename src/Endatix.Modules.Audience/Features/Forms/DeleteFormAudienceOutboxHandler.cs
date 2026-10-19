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
    /// <summary>The event types this work runs on, inline or as a job.</summary>
    public static readonly IReadOnlyCollection<string> HandledEventTypes = [FormDeletedEvent.EventTypeName];

    /// <inheritdoc />
    public IReadOnlyCollection<string> EventTypes => HandledEventTypes;

    /// <inheritdoc />
    public Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken) =>
        ProcessAsync(Parse(message), cancellationToken);

    /// <summary>The form the message deleted. Throws when the message cannot be read.</summary>
    internal static Input Parse(IOutboxMessage message)
    {
        using JsonDocument document = JsonDocument.Parse(message.Payload);
        return new Input(
            message.GetRequiredTenantId(document.RootElement),
            message.GetRequiredIdProp(document.RootElement, "formId"));
    }

    /// <summary>Soft-deletes the form's audience, in one transaction. A repeat matches nothing.</summary>
    internal async Task ProcessAsync(Input input, CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction =
            await ((DbContext)db).Database.BeginTransactionAsync(cancellationToken);
        await SoftDeleteFormAudienceAsync(input.TenantId, input.FormId, cancellationToken);
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

    internal sealed record Input(long TenantId, long FormId);
}
