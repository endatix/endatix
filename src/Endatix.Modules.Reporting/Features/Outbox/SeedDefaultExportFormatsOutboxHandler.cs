using System.Text.Json;
using Endatix.Core.Events;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Reporting.Features.ExportFormats;
using Endatix.Outbox.Engine;

namespace Endatix.Modules.Reporting.Features.Outbox;

/// <summary>
/// Provisions default <c>ExportFormats</c> rows for a new tenant.
/// <c>Tenant</c> is not <c>ITenantOwned</c>, so the outbox row is app-level
/// (<c>TenantId = 0</c>); the real id is only in the payload.
/// </summary>
internal sealed class SeedDefaultExportFormatsOutboxHandler(
    IDefaultExportFormatsSeeder seeder) : IOutboxIntegrationEventHandler
{
    public IReadOnlyCollection<string> EventTypes { get; } = [TenantCreatedEvent.EventTypeName];

    public async Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(message.Payload);
        var tenantId = message.GetRequiredIdProp(document.RootElement, "tenantId");
        await seeder.SeedAsync(tenantId, cancellationToken);
    }
}
