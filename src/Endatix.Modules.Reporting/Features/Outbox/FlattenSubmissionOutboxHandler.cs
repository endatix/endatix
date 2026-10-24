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
/// <remarks>
/// The same work runs inline in the relay or as a background job; both run the work <see cref="Read"/> returns.
/// </remarks>
internal sealed class FlattenSubmissionOutboxHandler(
    ISubmissionFlatteningProcessor flatteningProcessor,
    ILogger<FlattenSubmissionOutboxHandler> logger) : IOutboxWork<FlattenSubmissionOutboxHandler>
{
    public static string JobType => "ReportingFlattenSubmission";

    public static IReadOnlyCollection<string> HandledEventTypes { get; } =
        [SubmissionCompletedEvent.EventTypeName, SubmissionUpdatedEvent.EventTypeName];

    // A flattened row holds the submission's data and its submitter profile fields, so a submitter change flattens it
    // again too. Core's data mask leaves the submitter out on purpose: its name describes the answers. A mark as real
    // must bring back the row a schema replace may have cleared while the submission was a test; flattening is
    // idempotent, so a mark as test flattens too.
    private const SubmissionChangeKinds FlattenedChanges =
        SubmissionChangeKindsMasks.SubmissionData | SubmissionChangeKinds.Submitter | SubmissionChangeKinds.Test;

    /// <inheritdoc />
    public OutboxWork Read(IOutboxMessage message) =>
        OutboxWork.If(Parse(message, logger), ProcessAsync);

    /// <summary>
    /// Reads the work from the message, or <see langword="null"/> when the change it reports does not affect the
    /// submission's data and there is nothing to flatten. Throws <see cref="InvalidOperationException"/> when the
    /// message cannot be read.
    /// </summary>
    public static Input? Parse(IOutboxMessage message, ILogger logger)
    {
        using var document = JsonDocument.Parse(message.Payload);
        var payload = document.RootElement;

        if (ShouldSkipDueToChangeKind(message, payload, logger))
        {
            return null;
        }

        return new Input(
            message.GetRequiredTenantId(payload),
            message.GetRequiredIdProp(payload, "formId"),
            message.GetRequiredIdProp(payload, "submissionId"));
    }

    public Task ProcessAsync(Input input, CancellationToken cancellationToken) =>
        flatteningProcessor.ProcessAsync(input.TenantId, input.FormId, input.SubmissionId, cancellationToken);

    private static bool ShouldSkipDueToChangeKind(IOutboxMessage message, JsonElement payload, ILogger logger)
    {
        if (message.EventType != SubmissionUpdatedEvent.EventTypeName)
        {
            return false;
        }

        var changeKindWireValue = JsonElementReader.TryGetString(payload, "changeKind");
        var changeKind = SubmissionChangeKindsExtensions.ParseWireValue(changeKindWireValue);

        if ((changeKind & FlattenedChanges) != SubmissionChangeKinds.None)
        {
            return false;
        }

        logger.LogDebug(
            "Skipping submission flatten for outbox message {OutboxMessageId}: changeKind '{ChangeKind}' does not affect flattened data",
            message.Id,
            changeKindWireValue ?? string.Empty);

        return true;
    }

    public sealed record Input(long TenantId, long FormId, long SubmissionId);
}
