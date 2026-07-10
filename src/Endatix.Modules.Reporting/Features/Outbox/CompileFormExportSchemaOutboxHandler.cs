using System.Text.Json;
using Endatix.Core.Events;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Outbox.Engine;

namespace Endatix.Modules.Reporting.Features.Outbox;

/// <summary>
/// Handles the form definition updated event by compiling the form export schema.
/// </summary>
internal sealed class CompileFormExportSchemaOutboxHandler(
    IFormSchemaProcessor schemaProcessor) : IOutboxIntegrationEventHandler
{
    public IReadOnlyCollection<string> EventTypes { get; } = [FormDefinitionUpdatedEvent.EventTypeName];

    /// <inheritdoc />
    public async Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(message.Payload);
        var payload = document.RootElement;

        var tenantId = message.GetRequiredTenantId(payload);
        var formId = message.GetRequiredIdProp(payload, "formId");
        var formDefinitionId = message.GetRequiredIdProp(payload, "formDefinitionId");

        await schemaProcessor.ProcessAsync(tenantId, formId, formDefinitionId, cancellationToken);
    }
}
