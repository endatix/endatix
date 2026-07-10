using System.Text.Json;
using Endatix.Core.Events;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Utils;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Reporting.Features.Outbox;

/// <summary>
/// Handles the submission completed and updated events by flattening the submission into the reporting flattened read model.
/// </summary>
internal sealed class FlattenSubmissionOutboxHandler(
    ISubmissionFlatteningProcessor flatteningProcessor,
    ILogger<FlattenSubmissionOutboxHandler> logger) : IOutboxIntegrationEventHandler
{
    public IReadOnlyCollection<string> EventTypes { get; } =
        [SubmissionCompletedEvent.EventTypeName, SubmissionUpdatedEvent.EventTypeName];

    /// <inheritdoc />
    public async Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(message.Payload);
        var payload = document.RootElement;

        if (ShouldSkipDueToChangeKind(message, payload))
        {
            return;
        }

        var tenantId = message.GetRequiredTenantId(payload);
        var formId = message.GetRequiredIdProp(payload, "formId");
        var submissionId = message.GetRequiredIdProp(payload, "submissionId");

        await flatteningProcessor.ProcessAsync(tenantId, formId, submissionId, cancellationToken);
    }

    private bool ShouldSkipDueToChangeKind(IOutboxMessage message, JsonElement payload)
    {
        if (message.EventType != SubmissionUpdatedEvent.EventTypeName)
        {
            return false;
        }

        var changeKindWireValue = JsonElementReader.TryGetString(payload, "changeKind");
        var changeKind = SubmissionChangeKindsExtensions.ParseWireValue(changeKindWireValue);

        if (changeKind.AffectsSubmissionData())
        {
            return false;
        }

        logger.LogDebug(
            "Skipping submission flatten for outbox message {OutboxMessageId}: changeKind '{ChangeKind}' does not affect submission data",
            message.Id,
            changeKindWireValue ?? string.Empty);

        return true;
    }
}
