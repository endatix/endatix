using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>
/// One outbox message, only when it belongs to <paramref name="tenantId"/>. Background work reads by the tenant it
/// runs for, because outside a request nothing else scopes the read.
/// </summary>
public sealed class OutboxMessageByIdForTenantSpec : SingleResultSpecification<OutboxMessage>
{
    public OutboxMessageByIdForTenantSpec(long outboxMessageId, long tenantId)
    {
        Query
            .AsNoTracking()
            .Where(message => message.Id == outboxMessageId && message.TenantId == tenantId);
    }
}
