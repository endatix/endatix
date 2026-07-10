using System.Text.Json;
using Endatix.Core.Events;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.Logging;
using FlattenedSubmissionRow = Endatix.Modules.Reporting.Domain.FlattenedSubmission;

namespace Endatix.Modules.Reporting.Features.Outbox;

/// <summary>
/// Handles the submission deleted event by marking the submission as deleted in the reporting flattened read model.
/// </summary>
internal sealed class SyncSubmissionDeletionOutboxHandler(
    IFlattenedSubmissionRepository flattenedSubmissionRepository,
    ILogger<SyncSubmissionDeletionOutboxHandler> logger) : IOutboxIntegrationEventHandler
{
    public IReadOnlyCollection<string> EventTypes { get; } = [SubmissionDeletedEvent.EventTypeName];

    /// <inheritdoc />
    public async Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(message.Payload);
        var payload = document.RootElement;

        var tenantId = message.GetRequiredTenantId(payload);
        var submissionId = message.GetRequiredIdProp(payload, "submissionId");

        var row = await flattenedSubmissionRepository.GetBySubmissionIdAsync(
            tenantId,
            submissionId,
            cancellationToken);
        if (row is null)
        {
            logger.LogDebug(
                "No flattened submission row for outbox message {OutboxMessageId}; deletion is a no-op",
                message.Id);
            return;
        }

        row.MarkDeleted();
        await flattenedSubmissionRepository.SaveAsync(row, cancellationToken);

        logger.LogDebug(
            "Marked flattened submission {SubmissionId} as deleted for outbox message {OutboxMessageId}",
            submissionId,
            message.Id);
    }
}
