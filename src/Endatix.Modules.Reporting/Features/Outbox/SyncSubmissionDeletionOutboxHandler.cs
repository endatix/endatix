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
    public static readonly IReadOnlyCollection<string> HandledEventTypes = [SubmissionDeletedEvent.EventTypeName];

    /// <inheritdoc />
    public IReadOnlyCollection<string> EventTypes => HandledEventTypes;

    /// <inheritdoc />
    public Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken) =>
        ProcessAsync(Parse(message), message.Id, cancellationToken);

    /// <summary>Reads the work from the message; throws <see cref="InvalidOperationException"/> when it cannot.</summary>
    public static Input Parse(IOutboxMessage message)
    {
        using var document = JsonDocument.Parse(message.Payload);
        var payload = document.RootElement;

        return new Input(message.GetRequiredTenantId(payload), message.GetRequiredIdProp(payload, "submissionId"));
    }

    public async Task ProcessAsync(Input input, long outboxMessageId, CancellationToken cancellationToken)
    {
        var (tenantId, submissionId) = input;

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
                outboxMessageId);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    public sealed record Input(long TenantId, long SubmissionId);
}
