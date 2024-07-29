using MediatR;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// A domain event that is dispatched whenever a new submission is completed.
/// The <c>CreateSubmissionHandler</c> is used to dispatch this event.
/// </summary>
public sealed class SubmissionCompletedEvent(Submission submission) : DomainEventBase
{
    public Submission Submission { get; init; } = submission;
}