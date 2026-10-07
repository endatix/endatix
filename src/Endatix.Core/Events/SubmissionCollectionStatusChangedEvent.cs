using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Raised when a submission is screened out (<c>screen_out</c>). A normal complete stays on
/// <see cref="SubmissionCompletedEvent"/> and does not raise this. Cancellation does not raise it.
/// </summary>
public sealed class SubmissionCollectionStatusChangedEvent(Submission submission, CollectionStatus? previousCollectionStatus)
    : DomainEventBase, IIntegrationEvent
{
    public const string EventTypeName = "submission.collection_status_changed";

    public Submission Submission { get; init; } = submission;

    /// <summary>Collection status code before the change, e.g. <c>in_progress</c>.</summary>
    public string? PreviousCollectionStatus { get; } = previousCollectionStatus?.Code;

    public string EventType => EventTypeName;

    private readonly long _revision = submission.Revision;

    private readonly string _collectionStatus = submission.CollectionStatus.Code;

    public object GetPayload() => new Payload(Submission, _revision, _collectionStatus, PreviousCollectionStatus);

    /// <summary>
    /// Outbox contract for <c>submission.collection_status_changed</c> — adds <c>collectionStatus</c>
    /// and <c>previousCollectionStatus</c>.
    /// </summary>
    public sealed record Payload : SubmissionCompletedEvent.Payload
    {
        public string CollectionStatus { get; init; }

        public string? PreviousCollectionStatus { get; init; }

        public Payload(Submission submission, long revision, string collectionStatus, string? previousCollectionStatus)
            : base(submission, revision)
        {
            CollectionStatus = collectionStatus;
            PreviousCollectionStatus = previousCollectionStatus;
        }
    }
}
