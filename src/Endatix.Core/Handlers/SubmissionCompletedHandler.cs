using Microsoft.Extensions.Logging;
using Endatix.Core.Events;
using MediatR;

namespace Endatix.Core.Handlers;

internal sealed class SubmissionCompletedHandler(ILogger<SubmissionCompletedHandler> logger) : INotificationHandler<SubmissionCompletedEvent>
{
    public async Task Handle(SubmissionCompletedEvent domainEvent, CancellationToken cancellationToken)
    {
        logger.LogTrace("Handling Submission Created event for {@submissionId}", domainEvent.Submission);
    }
}