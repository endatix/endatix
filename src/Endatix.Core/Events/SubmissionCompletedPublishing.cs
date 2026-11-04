using Endatix.Core.Entities;
using MediatR;

namespace Endatix.Core.Events;

/// <summary>Publishes the in-process <see cref="SubmissionCompletedEvent"/> the same way from every use case.</summary>
public static class SubmissionCompletedPublishing
{
    /// <summary>Publishes <see cref="SubmissionCompletedEvent"/> for a saved, completed submission.</summary>
    public static Task PublishCompletedAsync(this IMediator mediator, Submission submission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(submission);
        return mediator.Publish(new SubmissionCompletedEvent(submission), cancellationToken);
    }
}
