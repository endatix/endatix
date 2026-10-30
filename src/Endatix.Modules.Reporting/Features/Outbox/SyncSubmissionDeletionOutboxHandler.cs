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
    ILogger<SyncSubmissionDeletionOutboxHandler> logger) : IOutboxWork<SyncSubmissionDeletionOutboxHandler>
{
    public static string JobType => "ReportingSyncSubmissionDeletion";

    public static IReadOnlyCollection<string> HandledEventTypes { get; } = [SubmissionDeletedEvent.EventTypeName];

    /// <inheritdoc />
    public OutboxWork Read(IOutboxMessage message)
    {
        var input = Parse(message);
        return OutboxWork.For(cancellationToken => ProcessAsync(input, message.Id, cancellationToken));
    }

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
        var deleted = await unitOfWork.InTransactionAsync(
            () => flattenedSubmissionRepository.DeleteBySubmissionIdAsync(tenantId, submissionId, cancellationToken),
            cancellationToken);

        logger.LogInformation(
            "Cleaned reporting flattened submission {SubmissionId} (deleted={Deleted}, outboxMessageId={OutboxMessageId})",
            submissionId,
            deleted,
            outboxMessageId);
    }

    public sealed record Input(long TenantId, long SubmissionId);
}
