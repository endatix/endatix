using Endatix.Core.Events;
using Endatix.Core.Features.WebHooks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Endatix.Samples.CustomEventHandlers.WebHooks.SubmissionEventHandlers;

internal class SubmissionCompletedHandler(IWebHookService webHookService, ILogger<SubmissionCompletedHandler> logger) : INotificationHandler<SubmissionCompletedEvent>
{
    public async Task Handle(SubmissionCompletedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling SubmissionCompletedEvent for submission {SubmissionId}", notification.Submission.Id);

        var submission = new
        {
            notification.Submission.Id,
            notification.Submission.FormId,
            notification.Submission.FormDefinitionId,
            notification.Submission.TenantId,
            notification.Submission.IsComplete,
            notification.Submission.JsonData,
            notification.Submission.CurrentPage,
            notification.Submission.Metadata,
            Status = notification.Submission.Status.Code,
            notification.Submission.CreatedAt,
            notification.Submission.ModifiedAt,
            notification.Submission.CompletedAt,
        };

        var message = new WebHookMessage<object>(
            notification.Submission.Id,
            WebHookOperation.SubmissionCompleted,
            submission);

        await webHookService.EnqueueWebHookAsync(notification.Submission.TenantId, message, cancellationToken);
    }
}
