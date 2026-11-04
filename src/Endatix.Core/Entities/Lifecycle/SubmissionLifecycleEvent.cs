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
        /// <summary>A submission was created.</summary>
        public const string Created = "submission.created";
        /// <summary>pending → active.</summary>
        public const string Started = "submission.started";
        /// <summary>The submission was completed. A webhook (<c>submission_completed</c>).</summary>
        public const string Completed = "submission.completed";
        /// <summary>The submission ended in a closed outcome. A webhook (<c>submission_closed</c>).</summary>
        public const string Closed = "submission.closed";
        /// <summary>pending or active → lapsed.</summary>
        public const string Lapsed = "submission.lapsed";
        /// <summary>lapsed → active.</summary>
        public const string Resumed = "submission.resumed";
        /// <summary>A move between two codes of one phase.</summary>
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

    internal SubmissionLifecycleEvent(Submission submission, CollectionStatus status, LifecycleChange change)
    {
        Submission = submission;
        Status = status;
        Change = change;
        _payload = Kind == SubmissionLifecycleEventKind.Completed
            ? SubmissionCompletedPayload.Raised(this)
            : SubmissionLifecyclePayload.Raised(this);
    }

    /// <summary>What happened.</summary>
    public SubmissionLifecycleEventKind Kind => Change.Kind;

    /// <summary>The status the submission is in after the change.</summary>
    internal CollectionStatus Status { get; }

    internal LifecycleChange Change { get; }

    /// <summary>The submission, read for its database-stamped fields when the event is written to the outbox.</summary>
    public Submission Submission { get; }

    /// <summary>The submission's revision this event raised it to.</summary>
    public long Revision => Change.Revision;

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
    /// <summary>The submission. Read when the event is written to the outbox.</summary>
    public long SubmissionId { get; init; }

    /// <summary>The submission's tenant.</summary>
    public long TenantId { get; init; }

    /// <summary>The submission's form.</summary>
    public long FormId { get; init; }

    /// <summary>The form definition the answers belong to.</summary>
    public long FormDefinitionId { get; init; }

    /// <summary>The submission's revision this event raised it to. One event, one revision.</summary>
    public long Revision { get; init; }

    /// <summary>When the event was raised (UTC).</summary>
    public DateTime OccurredAt { get; init; }

    /// <summary>Whether the submission was a test when the event was raised.</summary>
    public bool IsTestSubmission { get; init; }

    /// <summary>The status after the change.</summary>
    public CollectionStatusRef CollectionStatus { get; init; }

    /// <summary>The status before the change. Absent on <c>submission.created</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CollectionStatusRef? PreviousCollectionStatus { get; init; }

    internal static SubmissionLifecyclePayload Raised(SubmissionLifecycleEvent raised) =>
        new()
        {
            TenantId = raised.Submission.TenantId,
            FormId = raised.Submission.FormId,
            FormDefinitionId = raised.Submission.FormDefinitionId,
            Revision = raised.Revision,
            OccurredAt = raised.DateOccurred,
            IsTestSubmission = raised.Submission.IsTestSubmission,
            CollectionStatus = CollectionStatusRef.Of(raised.Status),
            PreviousCollectionStatus = raised.Change.Previous is { } previous ? CollectionStatusRef.Of(previous) : null,
        };
}

/// <summary>
/// The <c>submission.completed</c> payload: the lifecycle payload, the answers, and every field the
/// <c>submission_completed</c> webhook has always carried.
/// </summary>
public sealed record SubmissionCompletedPayload : SubmissionLifecyclePayload
{
    /// <summary>Whether the submission is complete.</summary>
    public bool IsComplete { get; init; }

    /// <summary>The answers.</summary>
    public string JsonData { get; init; } = null!;

    /// <summary>The page the respondent was on.</summary>
    public int? CurrentPage { get; init; }

    /// <summary>The submission metadata JSON.</summary>
    public string? Metadata { get; init; }

    /// <summary>Who submitted it, when known.</summary>
    public string? SubmittedBy { get; init; }

    /// <summary>The submitter's display id.</summary>
    public string? SubmitterDisplayId { get; init; }

    /// <summary>The reviewer status code.</summary>
    public string Status { get; init; } = null!;

    /// <summary>When the submission was created. Read when the event is written to the outbox.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>When the submission was last modified. Read when the event is written to the outbox.</summary>
    public DateTime? ModifiedAt { get; init; }

    /// <summary>When the respondent started.</summary>
    public DateTime? StartedAt { get; init; }

    /// <summary>When the submission was completed.</summary>
    public DateTime? CompletedAt { get; init; }

    internal static new SubmissionCompletedPayload Raised(SubmissionLifecycleEvent raised) =>
        new(SubmissionLifecyclePayload.Raised(raised))
        {
            IsComplete = raised.Submission.IsComplete,
            JsonData = raised.Submission.JsonData,
            CurrentPage = raised.Submission.CurrentPage,
            Metadata = raised.Submission.Metadata,
            SubmittedBy = raised.Submission.SubmittedBy,
            SubmitterDisplayId = raised.Submission.SubmitterDisplayId,
            Status = raised.Submission.Status.Code,
            StartedAt = raised.Submission.StartedAt,
            CompletedAt = raised.Submission.CompletedAt,
        };

    private SubmissionCompletedPayload(SubmissionLifecyclePayload core) : base(core)
    {
    }
}
