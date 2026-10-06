using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Raised when collection status changes to a terminal fielding outcome such as <c>screen_out</c>.
/// A normal complete stays on <see cref="SubmissionCompletedEvent"/> and does not raise this.
/// </summary>
public sealed class SubmissionCollectionStatusChangedEvent(Submission submission) : DomainEventBase, IIntegrationEvent
{
    public const string EventTypeName = "submission.collection_status_changed";

    public Submission Submission { get; init; } = submission;

    public string EventType => EventTypeName;

    private readonly long _revision = submission.Revision;

    public object GetPayload() => new Payload(Submission, _revision);

    public sealed record Payload
    {
        public long SubmissionId { get; init; }
        public long FormId { get; init; }
        public long TenantId { get; init; }
        public string CollectionStatus { get; init; } = null!;
        public bool IsComplete { get; init; }
        public long Revision { get; init; }

        public Payload(Submission submission, long revision)
        {
            SubmissionId = submission.Id;
            FormId = submission.FormId;
            TenantId = submission.TenantId;
            CollectionStatus = submission.CollectionStatus.Code;
            IsComplete = submission.IsComplete;
            Revision = revision;
        }
    }
}
