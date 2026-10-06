using Endatix.Core.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.Handlers;

/// <summary>
/// In-process handler for a terminal collection outcome such as <c>screen_out</c>.
/// A normal complete stays on <see cref="SubmissionCompletedEvent"/>.
/// </summary>
internal sealed class SubmissionCollectionStatusChangedEventHandler(
    ILogger<SubmissionCollectionStatusChangedEventHandler> logger)
    : INotificationHandler<SubmissionCollectionStatusChangedEvent>
{
    public Task Handle(SubmissionCollectionStatusChangedEvent domainEvent, CancellationToken cancellationToken)
    {
        logger.LogTrace(
            "Handling collection status {CollectionStatus} for submission {SubmissionId}",
            domainEvent.Submission.CollectionStatus.Code,
            domainEvent.Submission.Id);

        return Task.CompletedTask;
    }
}
