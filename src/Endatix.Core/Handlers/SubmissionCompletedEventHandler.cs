using Microsoft.Extensions.Logging;
using Endatix.Core.Events;
using MediatR;

namespace Endatix.Core.Handlers;

/// <summary>
/// Default event handler for SubmissionCompletedEvent.
/// </summary>
internal sealed class SubmissionCompletedEventHandler(ILogger<SubmissionCompletedEventHandler> logger) : INotificationHandler<SubmissionCompletedEvent>
{
    public Task Handle(SubmissionCompletedEvent domainEvent, CancellationToken cancellationToken)
    {
        logger.LogSubmissionCompletedHandled(domainEvent.Submission);

        return Task.CompletedTask;
    }
}