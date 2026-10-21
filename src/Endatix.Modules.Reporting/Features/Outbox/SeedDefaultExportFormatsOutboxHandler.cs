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
/// <remarks>
/// The same work runs inline in the relay or as a background job; both run the work <see cref="Read"/> returns.
/// </remarks>
internal sealed class SeedDefaultExportFormatsOutboxHandler(IDefaultExportFormatsSeeder seeder)
    : IOutboxWork<SeedDefaultExportFormatsOutboxHandler>
{
    public static string JobType => "ReportingSeedDefaultExportFormats";

    public static IReadOnlyCollection<string> HandledEventTypes { get; } = [TenantCreatedEvent.EventTypeName];

    /// <inheritdoc />
    public OutboxWork Read(IOutboxMessage message) =>
        OutboxWork.For(Parse(message), ProcessAsync);

    /// <summary>
    /// The tenant being created, which owns the seeding. Throws <see cref="InvalidOperationException"/> when the
    /// message cannot be read.
    /// </summary>
    public static long Parse(IOutboxMessage message)
    {
        using var document = JsonDocument.Parse(message.Payload);
        return message.GetRequiredIdProp(document.RootElement, "tenantId");
    }

    public Task ProcessAsync(long tenantId, CancellationToken cancellationToken) =>
        seeder.SeedAsync(tenantId, cancellationToken);
}
