using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Endatix.Core.Features.Email;
using Endatix.Core.Events;
using MediatR;
using Endatix.Core.Features.WebHooks;
using Endatix.Core.Specifications;
using Endatix.Core.Entities;

namespace Endatix.Core.Handlers;

internal sealed class SubmissionCompletedHandler(IWebHookService<string> webHookService, ILogger<SubmissionCompletedHandler> logger) : INotificationHandler<SubmissionCompletedEvent>
{
    public async Task Handle(SubmissionCompletedEvent domainEvent, CancellationToken cancellationToken)
    {
        logger.LogTrace("Handling Submission Created event for {@submissionId}", domainEvent.Submission);

        var formSubmittedMessage = new WebHookMessage<string>(domainEvent.Submission.Id, "form_submitted", domainEvent.Submission.JsonData);

        await webHookService.EnqueueWebHookAsync(formSubmittedMessage, cancellationToken);
    }
}