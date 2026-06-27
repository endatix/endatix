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
/// <remarks>
/// Delivery is <b>synchronous</b> (<see cref="IWebHookService.DeliverWebHookAsync"/>): on any endpoint failure
/// this throws, so the engine relay reschedules/dead-letters the row and only marks it <c>Sent</c> after a real
/// delivery — at-least-once, with duplicates absorbed by the hook-id dedup header.
/// </remarks>
public sealed class WebHookIntegrationEventPublisher(
    IWebHookService webHookService,
    ILogger<WebHookIntegrationEventPublisher> logger) : IIntegrationEventPublisher
{
    // The webhook operations the relay delivers, keyed by their dotted contract EventType. The key is
    // derived from a single source (WebHookOperation.EventName) rather than re-declaring the dotted literals,
    // so a mistyped literal can't silently route to the "unmapped → skip" branch.
    private static readonly IReadOnlyDictionary<string, WebHookOperation> OperationsByEventType =
        new[]
        {
            WebHookOperation.FormCreated,
            WebHookOperation.FormUpdated,
            WebHookOperation.FormEnabledStateChanged,
            WebHookOperation.SubmissionCompleted,
            WebHookOperation.FormDeleted,
        }.ToDictionary(operation => ToContractEventType(operation.EventName), StringComparer.Ordinal);

    // Contract EventType = the operation's EventName with the entity/action separator (the FIRST underscore)
    // as a dot, leaving any further underscores in the action intact:
    // "form_created" → "form.created"; "form_enabled_state_changed" → "form.enabled_state_changed".
    private static string ToContractEventType(string eventName)
    {
        var separator = eventName.IndexOf('_');
        return separator < 0
            ? eventName
            : string.Concat(eventName.AsSpan(0, separator), ".", eventName.AsSpan(separator + 1));
    }

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

        // All five mapped operations are form-scoped — they need a formId for the per-form webhook config
        // lookup. A missing/unparsable formId is a malformed payload: throw so the relay retries (and
        // dead-letters at MaxAttempts) instead of silently delivering at tenant scope.
        var formId = TryGetFormId(payload)
            ?? throw new InvalidOperationException(
                $"Outbox message {message.Id} ({message.EventType}) is missing a valid formId.");

        var webHookMessage = new WebHookMessage<JsonElement>(message.Id, operation, payload);
        var delivered = await webHookService.DeliverWebHookAsync(
            message.TenantId, webHookMessage, cancellationToken, formId);

        if (!delivered)
        {
            // Throw so the relay treats this as a publish failure → reschedule (or dead-letter at MaxAttempts).
            // Re-delivery to already-succeeded endpoints is absorbed by the X-Endatix-Hook-Id dedup header.
            throw new InvalidOperationException(
                $"Webhook delivery failed for outbox message {message.Id} ({message.EventType}); it will be retried.");
        }
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
