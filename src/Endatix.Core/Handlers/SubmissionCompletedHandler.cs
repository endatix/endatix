using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Endatix.Core.Features.Email;
using Endatix.Core.Events;

namespace Endatix.Core.Handlers;

internal sealed class SubmissionCompletedHandler(IEmailSender emailSender, ILogger<SubmissionCompletedHandler> logger) : INotificationHandler<SubmissionCompletedEvent>
{
    public async Task Handle(SubmissionCompletedEvent domainEvent, CancellationToken cancellationToken)
    {
        logger.LogTrace("Handling Submission Created event for {@submissionId}", domainEvent.Submission);
    }
}