using Microsoft.Extensions.Logging;
using Endatix.Core.Events;
using MediatR;
using Endatix.Core.Features.WebHooks;
using Endatix.Core.UseCases.Submissions;

namespace Endatix.Core.Handlers;

/// <summary>
/// Handles the core Endatix logic on form submission completion.
/// </summary>
internal sealed class SubmissionCompletedHandler(IWebHookService webHookService, ILogger<SubmissionCompletedHandler> logger) : INotificationHandler<SubmissionCompletedEvent>
{
    /// <summary>
    /// Handles the SubmissionCompletedEvent by sending a WebHook notification.
    /// </summary>
    /// <param name="domainEvent">The SubmissionCompletedEvent to handle.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public async Task Handle(SubmissionCompletedEvent domainEvent, CancellationToken cancellationToken)
    {
        logger.LogTrace("Handling Submission Created event for {@submissionId}", domainEvent.Submission);

        var formSubmittedMessage = new WebHookMessage<SubmissionDto>(domainEvent.Submission.Id, WebHookOperation.FormSubmitted, SubmissionDto.FromSubmission(domainEvent.Submission));
        await webHookService.EnqueueWebHookAsync(formSubmittedMessage, cancellationToken);
    }
}