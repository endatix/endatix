using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Raised once when a submission is created, from every create entry point. Carries the initial collection status;
/// creating a submission raises no <see cref="SubmissionCollectionStatusChangedEvent"/>. Not a webhook.
/// </summary>
public sealed class SubmissionCreatedEvent(Submission submission) : DomainEventBase, IIntegrationEvent
{
    /// <summary>The outbox event type, <c>submission.created</c>.</summary>
    public const string EventTypeName = "submission.created";

    /// <summary>The new submission.</summary>
    public Submission Submission { get; init; } = submission;

    /// <inheritdoc />
    public string EventType => EventTypeName;

    private readonly SubmissionEventCapture _capture = SubmissionEventCapture.Of(submission);

    private readonly string _collectionStatus = submission.CollectionStatus.Code;

    /// <inheritdoc />
    public object GetPayload() => new Payload(Submission, _capture, _collectionStatus);

    /// <summary>Outbox contract for <c>submission.created</c>: the submission payload plus <c>collectionStatus</c>.</summary>
    public sealed record Payload : SubmissionCompletedEvent.Payload
    {
        /// <summary>Collection status the submission was created in, e.g. <c>in_progress</c> or <c>not_started</c>.</summary>
        public string CollectionStatus { get; init; }

        /// <summary>Builds the payload from the submission and the state its event captured when raised.</summary>
        public Payload(Submission submission, SubmissionEventCapture capture, string collectionStatus)
            : base(submission, capture)
        {
            CollectionStatus = collectionStatus;
        }
    }
}
