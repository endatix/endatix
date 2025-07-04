using Endatix.Core.Events;
using Endatix.Core.Features.WebHooks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.WebHooks.Handlers;

/// <summary>
/// Webhook handler for SubmissionCompletedEvent.
/// </summary>
public class SubmissionCompletedWebHookHandler(IWebHookService webHookService, ILogger<SubmissionCompletedWebHookHandler> logger) : INotificationHandler<SubmissionCompletedEvent>
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
            WebHookOperation.FormSubmitted,
            submission);

        await webHookService.EnqueueWebHookAsync(message, cancellationToken);
    }
} 