using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Raised when a completed submission's material fields change, or when any submission is marked as test or real. Captured to the outbox for durable
/// reporting re-flatten and future stats subscribers (not a customer webhook in phase 1).
/// </summary>
public sealed class SubmissionUpdatedEvent(Submission submission, SubmissionChangeKinds changeKind)
    : DomainEventBase, IIntegrationEvent
{
    public const string EventTypeName = "submission.updated";

    public Submission Submission { get; init; } = submission;

    public SubmissionChangeKinds ChangeKind { get; } = changeKind;

    private readonly SubmissionEventCapture _capture = SubmissionEventCapture.Of(submission);

    public string EventType => EventTypeName;

    /// <inheritdoc />
    public object GetPayload() => new Payload(Submission, _capture, ChangeKind);

    /// <summary>Outbox contract for <c>submission.updated</c> — adds <c>changeKind</c>.</summary>
    public sealed record Payload : SubmissionCompletedEvent.Payload
    {
        public string ChangeKind { get; init; } = null!;

        /// <summary>Builds the payload from the submission and the state its event captured when raised.</summary>
        public Payload(Submission submission, SubmissionEventCapture capture, SubmissionChangeKinds changeKind)
            : base(submission, capture)
        {
            ChangeKind = changeKind.ToWireValue();
        }
    }
}
