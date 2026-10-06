using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Raised when a submission's review status changes. Not a collection-status event.
/// The wire name stays <c>submission.status_changed</c>.
/// </summary>
public sealed class SubmissionReviewStatusChangedEvent(Submission submission, SubmissionStatus previousStatus)
    : DomainEventBase, IIntegrationEvent
{
    public const string EventTypeName = "submission.status_changed";

    public Submission Submission { get; init; } = submission;

    public SubmissionStatus PreviousStatus { get; } = previousStatus;

    private readonly long _revision = submission.Revision;

    public string EventType => EventTypeName;

    public object GetPayload() => new Payload(Submission, _revision, PreviousStatus);

    /// <summary>Outbox contract for <c>submission.status_changed</c> — adds <c>previousStatus</c>.</summary>
    public sealed record Payload : SubmissionCompletedEvent.Payload
    {
        public string PreviousStatus { get; init; } = null!;

        public Payload(Submission submission, long revision, SubmissionStatus previousStatus)
            : base(submission, revision)
        {
            PreviousStatus = previousStatus.Code;
        }
    }
}
