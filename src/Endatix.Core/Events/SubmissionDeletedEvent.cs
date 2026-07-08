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

    public object GetPayload() => new
    {
        submissionId = Submission.Id,
        formId = Submission.FormId,
        tenantId = Submission.TenantId,
    };
}
