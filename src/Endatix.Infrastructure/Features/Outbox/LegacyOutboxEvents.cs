using System.Text.Json.Nodes;
using Endatix.Core.Entities;
using Endatix.Outbox.Engine;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// One release only (removal: endatix/endatix#1559): an outbox row an earlier version wrote as
/// <c>submission.collection_status_changed</c> is delivered as <c>submission.closed</c>. Earlier versions raised that
/// type only for a screen-out, so every such row is a closed screen-out. Both publishers and the webhook job read rows
/// through <see cref="Translate"/>, so a row left in the outbox, or a webhook job queued for one, still reaches its
/// subscribers and the <c>submission_closed</c> webhook.
/// </summary>
internal static class LegacyOutboxEvents
{
    /// <summary>The type earlier versions raised for a screen-out.</summary>
    public const string CollectionStatusChanged = "submission.collection_status_changed";

    /// <summary>Every legacy type this release translates.</summary>
    public static IReadOnlyCollection<string> All { get; } = [CollectionStatusChanged];

    /// <summary>The message as this version's subscribers expect it: a legacy row translated, any other unchanged.</summary>
    public static IOutboxMessage Translate(IOutboxMessage message) =>
        string.Equals(message.EventType, CollectionStatusChanged, StringComparison.Ordinal)
            ? new TranslatedOutboxMessage(message, SubmissionLifecycleEvent.EventTypes.Closed, ClosedPayload(message))
            : message;

    // The lifecycle payload of submission.closed: the old row's identifiers and revision, the screen-out as
    // { code, phase }, the previous status the same way, and the row's time. Answers are not part of submission.closed.
    private static string ClosedPayload(IOutboxMessage message)
    {
        var old = JsonNode.Parse(message.Payload)?.AsObject() ?? [];
        var closed = CoreFields(old);
        closed["occurredAt"] = message.OccurredAt.UtcDateTime.ToString("O");
        closed["collectionStatus"] = StatusRef(CollectionStatusCodes.ScreenOut);
        if (old["previousCollectionStatus"]?.GetValue<string>() is { Length: > 0 } previous)
        {
            closed["previousCollectionStatus"] = StatusRef(previous);
        }

        return closed.ToJsonString();
    }

    private static JsonObject CoreFields(JsonObject old)
    {
        JsonObject core = [];
        foreach (var field in CoreFieldNames.Where(old.ContainsKey))
        {
            core[field] = old[field]?.DeepClone();
        }

        return core;
    }

    private static readonly string[] CoreFieldNames =
        ["submissionId", "tenantId", "formId", "formDefinitionId", "revision", "isTestSubmission"];

    private static JsonObject StatusRef(string code)
    {
        var status = CollectionStatus.FromCode(code);
        return new JsonObject { ["code"] = status.Code, ["phase"] = status.Phase.ToWireName() };
    }

    private sealed class TranslatedOutboxMessage(IOutboxMessage row, string eventType, string payload) : IOutboxMessage
    {
        public long Id => row.Id;

        public string EventType => eventType;

        public string Payload => payload;

        public long TenantId => row.TenantId;

        public DateTimeOffset OccurredAt => row.OccurredAt;

        public int SchemaVersion => row.SchemaVersion;

        public int Attempts => row.Attempts;

        public string? TraceId => row.TraceId;
    }
}
