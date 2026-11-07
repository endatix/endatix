using Endatix.Core.Features.WebHooks;
using Endatix.Infrastructure.Utils;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// The outbox event types that are delivered to webhooks, and the webhook operation each one is. Delivery depends on
/// the event type only; the payload is never read to decide it. A webhook's outbox type is its name with the first
/// <c>_</c> turned into <c>.</c> (<c>submission_closed</c> → <c>submission.closed</c>), and its settings key is the
/// name in PascalCase (<c>SubmissionClosed</c>).
/// </summary>
internal static class WebHookEvents
{
    private static readonly IReadOnlyDictionary<string, WebHookOperation> _operationsByEventType =
        new[]
        {
            WebHookOperation.FormCreated,
            WebHookOperation.FormUpdated,
            WebHookOperation.FormEnabledStateChanged,
            WebHookOperation.SubmissionCompleted,
            WebHookOperation.SubmissionClosed,
            WebHookOperation.FormDeleted,
        }.ToDictionary(operation => StringUtils.ToDottedEventType(operation.EventName), StringComparer.Ordinal);

    /// <summary>The outbox event types webhook delivery subscribes to.</summary>
    public static IReadOnlyCollection<string> EventTypes { get; } = [.. _operationsByEventType.Keys];

    /// <summary>The webhook operation an outbox event type is delivered as, if it is a webhook event.</summary>
    public static bool TryGetOperation(string eventType, out WebHookOperation operation) =>
        _operationsByEventType.TryGetValue(eventType, out operation!);
}
