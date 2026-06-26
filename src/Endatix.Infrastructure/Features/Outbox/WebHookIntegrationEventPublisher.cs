using System.Text.Json;
using Endatix.Core.Features.WebHooks;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// Stage-1 (in-process, no DAPR) <see cref="IIntegrationEventPublisher"/>: turns a claimed outbox row back
/// into the existing webhook delivery path (<see cref="IWebHookService"/> → <c>WebHookServer</c>). It maps the
/// stored dotted <c>EventType</c> to the matching <see cref="WebHookOperation"/>, forwards the stored JSON
/// payload verbatim, and reuses the outbox <c>Id</c> as the webhook id so the <c>X-Endatix-Hook-Id</c> dedup
/// header is stable across relay retries.
/// </summary>
public sealed class WebHookIntegrationEventPublisher(
    IWebHookService webHookService,
    ILogger<WebHookIntegrationEventPublisher> logger) : IIntegrationEventPublisher
{
    // Dotted contract EventType → existing WebHookOperation (whose EventName keeps the underscore form the
    // per-form webhook config lookup expects). These 5 are the Phase-3 slice events.
    private static readonly IReadOnlyDictionary<string, WebHookOperation> OperationsByEventType =
        new Dictionary<string, WebHookOperation>(StringComparer.Ordinal)
        {
            ["form.created"] = WebHookOperation.FormCreated,
            ["form.updated"] = WebHookOperation.FormUpdated,
            ["form.enabled_state_changed"] = WebHookOperation.FormEnabledStateChanged,
            ["submission.completed"] = WebHookOperation.SubmissionCompleted,
            ["form.deleted"] = WebHookOperation.FormDeleted,
        };

    /// <inheritdoc />
    public async Task PublishAsync(IOutboxMessage message, CancellationToken cancellationToken)
    {
        if (!OperationsByEventType.TryGetValue(message.EventType, out var operation))
        {
            // Unmapped type: nothing to deliver. Return so the relay marks it Sent (no retry storm).
            logger.LogWarning(
                "Outbox message {MessageId} has no webhook mapping for event type '{EventType}'; skipping delivery.",
                message.Id, message.EventType);
            return;
        }

        // Parse to a self-contained JsonElement (the clone survives the JsonDocument being disposed, so it
        // stays valid on the background webhook queue). WebHookServer re-serializes it verbatim.
        using var document = JsonDocument.Parse(message.Payload);
        var payload = document.RootElement.Clone();

        var webHookMessage = new WebHookMessage<JsonElement>(message.Id, operation, payload);
        await webHookService.EnqueueWebHookAsync(
            message.TenantId, webHookMessage, cancellationToken, TryGetFormId(payload));
    }

    // formId drives the per-form webhook config lookup. IDs are stored as strings on the wire
    // (LongToStringConverter), but tolerate a numeric encoding too.
    private static long? TryGetFormId(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("formId", out var formId))
        {
            return null;
        }

        return formId.ValueKind switch
        {
            JsonValueKind.String when long.TryParse(formId.GetString(), out var id) => id,
            JsonValueKind.Number when formId.TryGetInt64(out var id) => id,
            _ => null,
        };
    }
}
