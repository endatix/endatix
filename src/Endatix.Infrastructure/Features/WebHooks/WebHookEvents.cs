using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Features.WebHooks;
using Endatix.Infrastructure.Utils;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// The outbox event types that are delivered to webhooks, and the webhook operation each one is.
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
            WebHookOperation.SubmissionCollectionStatusChanged,
            WebHookOperation.FormDeleted,
        }.ToDictionary(operation => StringUtils.ToDottedEventType(operation.EventName), StringComparer.Ordinal);

    // submission_collection_status_changed is a terminal fielding outcome. A complete is submission_completed.
    private static readonly HashSet<string> _deliveredCollectionStatuses = new(StringComparer.Ordinal)
    {
        CollectionStatusCodes.ScreenOut,
        CollectionStatusCodes.QuotaFull,
        CollectionStatusCodes.Expired,
        CollectionStatusCodes.Abandoned,
        CollectionStatusCodes.Cancelled,
    };

    private static readonly string _collectionStatusChangedEventType =
        StringUtils.ToDottedEventType(WebHookOperation.SubmissionCollectionStatusChanged.EventName);

    /// <summary>The outbox event types webhook delivery subscribes to.</summary>
    public static IReadOnlyCollection<string> EventTypes { get; } = [.. _operationsByEventType.Keys];

    /// <summary>
    /// Whether an outbox message of this event type and payload is delivered as a webhook, and as which operation.
    /// A collection status change is delivered only for a terminal outcome other than complete.
    /// </summary>
    public static bool TryGetOperation(string eventType, string payload, out WebHookOperation operation) =>
        _operationsByEventType.TryGetValue(eventType, out operation!)
        && (eventType != _collectionStatusChangedEventType || IsDeliveredCollectionStatus(payload));

    private static bool IsDeliveredCollectionStatus(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.TryGetProperty("collectionStatus", out var status)
            && status.ValueKind == JsonValueKind.String
            && _deliveredCollectionStatuses.Contains(status.GetString()!);
    }
}
