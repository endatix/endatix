using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Raised when a completed submission's material fields change. Captured to the outbox for durable
/// reporting re-flatten and future stats subscribers (not a customer webhook in phase 1).
/// </summary>
public sealed class SubmissionUpdatedEvent(Submission submission, SubmissionChangeKinds changeKind)
    : DomainEventBase, IIntegrationEvent
{
    public const string EventTypeName = "submission.updated";

    public Submission Submission { get; init; } = submission;

    public SubmissionChangeKinds ChangeKind { get; } = changeKind;

    private readonly long _revision = submission.Revision;

    public string EventType => EventTypeName;

    public object GetPayload() => new Payload(Submission, _revision, ChangeKind);

    /// <summary>Outbox contract for <c>submission.updated</c> — adds <c>changeKind</c>.</summary>
    public sealed record Payload : SubmissionCompletedEvent.Payload
    {
        public string ChangeKind { get; init; } = null!;

        public Payload(Submission submission, long revision, SubmissionChangeKinds changeKind)
            : base(submission, revision)
        {
            ChangeKind = changeKind.ToWireValue();
        }
    }
}
