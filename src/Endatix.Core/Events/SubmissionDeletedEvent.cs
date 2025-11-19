using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Event dispatched when a submission is deleted.
/// </summary>
public sealed class SubmissionDeletedEvent(Submission submission) : DomainEventBase
{
    public Submission Submission { get; init; } = submission;
}