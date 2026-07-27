using System.Text.Json;
using Endatix.Core.Events;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Reporting.Data;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Reporting.Features.Outbox;

/// <summary>
/// Hard-deletes the reporting flattened submission row when a submission is deleted.
/// </summary>
internal sealed class SyncSubmissionDeletionOutboxHandler(
    IFlattenedSubmissionRepository flattenedSubmissionRepository,
    IReportingUnitOfWork unitOfWork,
    ILogger<SyncSubmissionDeletionOutboxHandler> logger) : IOutboxIntegrationEventHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<string> EventTypes { get; } = [SubmissionDeletedEvent.EventTypeName];

    /// <inheritdoc />
    public async Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(message.Payload);
        var payload = document.RootElement;

        var tenantId = message.GetRequiredTenantId(payload);
        var submissionId = message.GetRequiredIdProp(payload, "submissionId");

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var deleted = await flattenedSubmissionRepository.DeleteBySubmissionIdAsync(
                tenantId,
                submissionId,
                cancellationToken);

            await unitOfWork.CommitTransactionAsync(cancellationToken);

            logger.LogInformation(
                "Cleaned reporting flattened submission {SubmissionId} (deleted={Deleted}, outboxMessageId={OutboxMessageId})",
                submissionId,
                deleted,
                message.Id);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }
}
