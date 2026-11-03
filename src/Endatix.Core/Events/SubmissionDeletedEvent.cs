using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Raised when a submission is deleted. Captured to the outbox for durable flattened-row cleanup.
/// </summary>
public sealed class SubmissionDeletedEvent(Submission submission) : DomainEventBase, IIntegrationEvent
{
    public const string EventTypeName = "submission.deleted";

    public Submission Submission { get; init; } = submission;

    public string EventType => EventTypeName;

    private readonly bool _isTestSubmission = submission.IsTestSubmission;

    /// <inheritdoc />
    public object GetPayload() => new Payload(Submission, _isTestSubmission);

    /// <summary>Outbox contract for <c>submission.deleted</c>.</summary>
    public sealed record Payload
    {
        public long SubmissionId { get; init; }

        public long FormId { get; init; }

        public long TenantId { get; init; }

        /// <summary>Whether the submission was a test when the event was raised.</summary>
        public bool IsTestSubmission { get; init; }

        /// <summary>Builds the payload from the submission and its test flag when the event was raised.</summary>
        public Payload(Submission submission, bool isTestSubmission)
        {
            SubmissionId = submission.Id;
            FormId = submission.FormId;
            TenantId = submission.TenantId;
            IsTestSubmission = isTestSubmission;
        }
    }
}
