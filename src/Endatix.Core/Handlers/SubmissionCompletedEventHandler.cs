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
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("Handling Submission Created event for {@EventData}", domainEvent.Submission);
        }

        return Task.CompletedTask;
    }
}