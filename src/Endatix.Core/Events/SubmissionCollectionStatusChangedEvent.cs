using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Retired: nothing raises it. A collection status change raises one <see cref="SubmissionLifecycleEvent"/> named by
/// its phases; a screen-out is <see cref="SubmissionLifecycleEvent.EventTypes.Closed"/>.
/// </summary>
[Obsolete("Retired: subscribe to SubmissionLifecycleEvent.EventTypes (a screen-out is EventTypes.Closed) and read SubmissionLifecyclePayload.", error: true)]
public sealed class SubmissionCollectionStatusChangedEvent(Submission submission, CollectionStatus? previousCollectionStatus)
    : DomainEventBase, IIntegrationEvent
{
    /// <summary>The retired outbox event type.</summary>
    [Obsolete("Retired: use SubmissionLifecycleEvent.EventTypes.Closed for a screen-out.", error: true)]
    public const string EventTypeName = "submission.collection_status_changed";

    public Submission Submission { get; init; } = submission;

    /// <summary>Collection status code before the change, e.g. <c>in_progress</c>.</summary>
    public string? PreviousCollectionStatus { get; } = previousCollectionStatus?.Code;

    public string EventType => EventTypeName;

    private readonly SubmissionEventCapture _capture = SubmissionEventCapture.Of(submission);

    private readonly string _collectionStatus = submission.CollectionStatus.Code;

    /// <inheritdoc />
    public object GetPayload() => new Payload(Submission, _capture, _collectionStatus, PreviousCollectionStatus);

    /// <summary>
    /// Outbox contract for <c>submission.collection_status_changed</c> — adds <c>collectionStatus</c>
    /// and <c>previousCollectionStatus</c>.
    /// </summary>
    public sealed record Payload : SubmissionCompletedEvent.Payload
    {
        public string CollectionStatus { get; init; }

        public string? PreviousCollectionStatus { get; init; }

        /// <summary>Builds the payload from the submission and the state its event captured when raised.</summary>
        public Payload(Submission submission, SubmissionEventCapture capture, string collectionStatus, string? previousCollectionStatus)
            : base(submission, capture)
        {
            CollectionStatus = collectionStatus;
            PreviousCollectionStatus = previousCollectionStatus;
        }
    }
}
