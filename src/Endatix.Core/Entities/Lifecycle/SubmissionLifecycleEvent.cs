using System.Text.Json.Serialization;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

/// <summary>What happened to a submission's collection, by the phases before and after the change.</summary>
public enum SubmissionLifecycleEventKind
{
    /// <summary>The submission was created; carries the status it was saved in.</summary>
    Created,

    /// <summary>pending → active.</summary>
    Started,

    /// <summary>Any open phase → completed.</summary>
    Completed,

    /// <summary>Any open phase → closed.</summary>
    Closed,

    /// <summary>pending or active → lapsed.</summary>
    Lapsed,

    /// <summary>lapsed → active.</summary>
    Resumed,

    /// <summary>A move between two codes of one open phase, e.g. <c>not_started</c> → <c>viewed</c>.</summary>
    CollectionStatusMoved,
}

/// <summary>
/// The one integration event for the submission lifecycle. Each lifecycle change raises exactly one, and its
/// <see cref="EventType"/> names the change. Subscribe by event type; the payload carries the exact code and phase.
/// Only the submission entity raises it.
/// </summary>
/// <remarks>
/// Every payload field is taken when the event is raised, except the ones the database stamps on save:
/// <c>submissionId</c>, and on <c>submission.completed</c> <c>createdAt</c> and <c>modifiedAt</c>.
/// </remarks>
public sealed class SubmissionLifecycleEvent : DomainEventBase, IIntegrationEvent
{
    /// <summary>The outbox event types of the submission lifecycle.</summary>
    public static class EventTypes
    {
        public const string Created = "submission.created";
        public const string Started = "submission.started";
        public const string Completed = "submission.completed";
        public const string Closed = "submission.closed";
        public const string Lapsed = "submission.lapsed";
        public const string Resumed = "submission.resumed";
        public const string CollectionStatusMoved = "submission.collection_status_moved";

        /// <summary>Every lifecycle event type, one per <see cref="SubmissionLifecycleEventKind"/>.</summary>
        public static IReadOnlyList<string> All { get; } =
            [.. Enum.GetValues<SubmissionLifecycleEventKind>().Select(Of)];

        /// <summary>The event type of a kind.</summary>
        public static string Of(SubmissionLifecycleEventKind kind) => kind switch
        {
            SubmissionLifecycleEventKind.Created => Created,
            SubmissionLifecycleEventKind.Started => Started,
            SubmissionLifecycleEventKind.Completed => Completed,
            SubmissionLifecycleEventKind.Closed => Closed,
            SubmissionLifecycleEventKind.Lapsed => Lapsed,
            SubmissionLifecycleEventKind.Resumed => Resumed,
            SubmissionLifecycleEventKind.CollectionStatusMoved => CollectionStatusMoved,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown lifecycle event kind."),
        };
    }

    private readonly SubmissionLifecyclePayload _payload;

    internal SubmissionLifecycleEvent(
        SubmissionLifecycleEventKind kind,
        Submission submission,
        CollectionStatus status,
        CollectionStatus? previous,
        long revision)
    {
        Kind = kind;
        Submission = submission;
        Revision = revision;
        _payload = kind == SubmissionLifecycleEventKind.Completed
            ? SubmissionCompletedPayload.Raised(submission, status, previous, revision, DateOccurred)
            : SubmissionLifecyclePayload.Raised(submission, status, previous, revision, DateOccurred);
    }

    /// <summary>What happened.</summary>
    public SubmissionLifecycleEventKind Kind { get; }

    /// <summary>The submission, read for its database-stamped fields when the event is written to the outbox.</summary>
    public Submission Submission { get; }

    /// <summary>The submission's revision this event raised it to.</summary>
    public long Revision { get; }

    /// <inheritdoc />
    public string EventType => EventTypes.Of(Kind);

    /// <inheritdoc />
    public object GetPayload() => _payload switch
    {
        SubmissionCompletedPayload completed => completed with
        {
            SubmissionId = Submission.Id,
            CreatedAt = Submission.CreatedAt,
            ModifiedAt = Submission.ModifiedAt,
        },
        _ => _payload with { SubmissionId = Submission.Id },
    };
}

/// <summary>A collection status on the wire: the exact code and its phase.</summary>
/// <param name="Code">The status code, e.g. <c>screen_out</c>.</param>
/// <param name="Phase">The phase wire name, e.g. <c>closed</c>.</param>
public readonly record struct CollectionStatusRef(string Code, string Phase)
{
    /// <summary>The wire form of a status.</summary>
    public static CollectionStatusRef Of(CollectionStatus status) => new(status.Code, status.Phase.ToWireName());
}

/// <summary>The payload every submission lifecycle event carries.</summary>
public record SubmissionLifecyclePayload
{
    public long SubmissionId { get; init; }

    public long TenantId { get; init; }

    public long FormId { get; init; }

    public long FormDefinitionId { get; init; }

    /// <summary>The submission's revision this event raised it to. One event, one revision.</summary>
    public long Revision { get; init; }

    public DateTime OccurredAt { get; init; }

    /// <summary>Whether the submission was a test when the event was raised.</summary>
    public bool IsTestSubmission { get; init; }

    public CollectionStatusRef CollectionStatus { get; init; }

    /// <summary>The status before the change. Absent on <c>submission.created</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CollectionStatusRef? PreviousCollectionStatus { get; init; }

    internal static SubmissionLifecyclePayload Raised(
        Submission submission,
        CollectionStatus status,
        CollectionStatus? previous,
        long revision,
        DateTime occurredAt) =>
        new()
        {
            TenantId = submission.TenantId,
            FormId = submission.FormId,
            FormDefinitionId = submission.FormDefinitionId,
            Revision = revision,
            OccurredAt = occurredAt,
            IsTestSubmission = submission.IsTestSubmission,
            CollectionStatus = CollectionStatusRef.Of(status),
            PreviousCollectionStatus = previous is null ? null : CollectionStatusRef.Of(previous),
        };
}

/// <summary>
/// The <c>submission.completed</c> payload: the lifecycle payload, the answers, and every field the
/// <c>submission_completed</c> webhook has always carried.
/// </summary>
public sealed record SubmissionCompletedPayload : SubmissionLifecyclePayload
{
    public bool IsComplete { get; init; }

    public string JsonData { get; init; } = null!;

    public int? CurrentPage { get; init; }

    public string? Metadata { get; init; }

    public string? SubmittedBy { get; init; }

    public string? SubmitterDisplayId { get; init; }

    /// <summary>The reviewer status code.</summary>
    public string Status { get; init; } = null!;

    public DateTime CreatedAt { get; init; }

    public DateTime? ModifiedAt { get; init; }

    public DateTime? StartedAt { get; init; }

    public DateTime? CompletedAt { get; init; }

    internal static new SubmissionCompletedPayload Raised(
        Submission submission,
        CollectionStatus status,
        CollectionStatus? previous,
        long revision,
        DateTime occurredAt) =>
        new(SubmissionLifecyclePayload.Raised(submission, status, previous, revision, occurredAt))
        {
            IsComplete = submission.IsComplete,
            JsonData = submission.JsonData,
            CurrentPage = submission.CurrentPage,
            Metadata = submission.Metadata,
            SubmittedBy = submission.SubmittedBy,
            SubmitterDisplayId = submission.SubmitterDisplayId,
            Status = submission.Status.Code,
            StartedAt = submission.StartedAt,
            CompletedAt = submission.CompletedAt,
        };

    private SubmissionCompletedPayload(SubmissionLifecyclePayload core) : base(core)
    {
    }
}
