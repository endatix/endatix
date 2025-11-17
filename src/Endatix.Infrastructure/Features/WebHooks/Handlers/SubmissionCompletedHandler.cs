using Endatix.Core.Abstractions;
using Endatix.Core.Events;
using Endatix.Core.Features.WebHooks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.WebHooks.Handlers;

/// <summary>
/// Webhook handler for SubmissionCompletedEvent.
/// </summary>
public class SubmissionCompletedWebHookHandler(IWebHookService webHookService, ILogger<SubmissionCompletedWebHookHandler> logger, IIdGenerator<long> idGenerator) : INotificationHandler<SubmissionCompletedEvent>
{
    public async Task Handle(SubmissionCompletedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling SubmissionCompletedEvent for submission {SubmissionId}", notification.Submission.Id);

        var submission = new
        {
            submissionId = notification.Submission.Id,
            formId = notification.Submission.FormId,
            formDefinitionId = notification.Submission.FormDefinitionId,
            tenantId = notification.Submission.TenantId,
            isComplete = notification.Submission.IsComplete,
            jsonData = notification.Submission.JsonData,
            currentPage = notification.Submission.CurrentPage,
            metadata = notification.Submission.Metadata,
            submittedBy = notification.Submission.SubmittedBy,
            status = notification.Submission.Status.Code,
            createdAt = notification.Submission.CreatedAt,
            modifiedAt = notification.Submission.ModifiedAt,
            completedAt = notification.Submission.CompletedAt,
        };

        var message = new WebHookMessage<object>(
            idGenerator.CreateId(),
            WebHookOperation.SubmissionCompleted,
            submission);

        await webHookService.EnqueueWebHookAsync(notification.Submission.TenantId, message, cancellationToken, notification.Submission.FormId);
    }
} 