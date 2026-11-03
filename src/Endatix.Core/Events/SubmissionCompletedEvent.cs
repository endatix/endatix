using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// A domain event dispatched whenever a submission is completed. Also an <see cref="IIntegrationEvent"/> —
/// captured to the outbox and delivered as the <c>submission.completed</c> webhook by the relay.
/// </summary>
public sealed class SubmissionCompletedEvent(Submission submission) : DomainEventBase, IIntegrationEvent
{
    public const string EventTypeName = "submission.completed";

    public Submission Submission { get; init; } = submission;

    /// <inheritdoc />
    public string EventType => EventTypeName;

    private readonly SubmissionEventCapture _capture = SubmissionEventCapture.Of(submission);

    /// <inheritdoc />
    public object GetPayload() => new Payload(Submission, _capture);

    /// <summary>
    /// Base outbox/webhook contract for submission integration events.
    /// Captures an immutable snapshot of the submission at the exact moment the event occurred.
    /// </summary>
    public record Payload
    {
        public long SubmissionId { get; init; }

        public long FormId { get; init; }

        public long FormDefinitionId { get; init; }

        public long TenantId { get; init; }

        public bool IsComplete { get; init; }

        public string JsonData { get; init; } = null!;

        public int? CurrentPage { get; init; }

        public string? Metadata { get; init; }

        public string? SubmittedBy { get; init; }

        public string? SubmitterDisplayId { get; init; }

        public string Status { get; init; } = null!;

        public DateTime CreatedAt { get; init; }

        public DateTime? ModifiedAt { get; init; }

        public DateTime? StartedAt { get; init; }

        public DateTime? CompletedAt { get; init; }

        public long Revision { get; init; }

        /// <summary>Whether the submission was a test when the event was raised.</summary>
        public bool IsTestSubmission { get; init; }

        protected Payload()
        {
        }

        /// <summary>Builds the payload from the submission and the state its event captured when raised.</summary>
        public Payload(Submission submission, SubmissionEventCapture capture)
        {
            SubmissionId = submission.Id;
            FormId = submission.FormId;
            FormDefinitionId = submission.FormDefinitionId;
            TenantId = submission.TenantId;
            IsComplete = submission.IsComplete;
            JsonData = submission.JsonData;
            CurrentPage = submission.CurrentPage;
            Metadata = submission.Metadata;
            SubmittedBy = submission.SubmittedBy;
            SubmitterDisplayId = submission.SubmitterDisplayId;
            Status = submission.Status.Code;
            CreatedAt = submission.CreatedAt;
            ModifiedAt = submission.ModifiedAt;
            StartedAt = submission.StartedAt;
            CompletedAt = submission.CompletedAt;
            Revision = capture.Revision;
            IsTestSubmission = capture.IsTestSubmission;
        }
    }
}
