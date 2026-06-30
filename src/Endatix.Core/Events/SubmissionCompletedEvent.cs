using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// A domain event dispatched whenever a submission is completed. Also an <see cref="IIntegrationEvent"/> —
/// captured to the outbox and delivered as the <c>submission.completed</c> webhook by the relay.
/// </summary>
public sealed class SubmissionCompletedEvent(Submission submission) : DomainEventBase, IIntegrationEvent
{
    public Submission Submission { get; init; } = submission;

    /// <inheritdoc />
    public string EventType => "submission.completed";

    /// <inheritdoc />
    public object GetPayload() => new
    {
        submissionId = Submission.Id,
        formId = Submission.FormId,
        formDefinitionId = Submission.FormDefinitionId,
        tenantId = Submission.TenantId,
        isComplete = Submission.IsComplete,
        jsonData = Submission.JsonData,
        currentPage = Submission.CurrentPage,
        metadata = Submission.Metadata,
        submittedBy = Submission.SubmittedBy,
        submitterDisplayId = Submission.SubmitterDisplayId,
        status = Submission.Status.Code,
        createdAt = Submission.CreatedAt,
        modifiedAt = Submission.ModifiedAt,
        completedAt = Submission.CompletedAt,
        revision = Submission.Revision,
    };
}