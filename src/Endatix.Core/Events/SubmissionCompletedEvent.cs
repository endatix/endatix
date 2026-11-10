using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// The in-process notification that a submission was completed, published through MediatR by the create and update
/// use cases after the save (<see cref="SubmissionCompletedPublishing.PublishCompletedAsync"/>). It never reaches the
/// outbox: the outbox event is <see cref="SubmissionLifecycleEvent"/> with type
/// <see cref="SubmissionLifecycleEvent.EventTypes.Completed"/>.
/// </summary>
public sealed class SubmissionCompletedEvent(Submission submission) : DomainEventBase
{
    /// <summary>The outbox event type of a completion.</summary>
#pragma warning disable S1133 // Kept obsolete for callers-only / tombstone policy (Loop A #1472); removal is a separate decision.
    [Obsolete("This event no longer reaches the outbox. Use SubmissionLifecycleEvent.EventTypes.Completed.")]
#pragma warning restore S1133
    public const string EventTypeName = SubmissionLifecycleEvent.EventTypes.Completed;

    public Submission Submission { get; init; } = submission;

    /// <summary>
    /// The base payload of <c>submission.updated</c> and <c>submission.status_changed</c>. The
    /// <c>submission.completed</c> payload is <see cref="SubmissionCompletedPayload"/>.
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
