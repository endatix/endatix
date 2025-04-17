using Microsoft.Extensions.Logging;
using Endatix.Core.Events;
using MediatR;
using Endatix.Core.Features.Email;

namespace Endatix.Core.Handlers;

/// <summary>
/// Default event handler for UserRegisteredEvent.
/// </summary>
internal sealed class UserRegisteredEventHandler(IEmailSender emailSender, ILogger<UserRegisteredEventHandler> logger) : INotificationHandler<UserRegisteredEvent>
{
    public async Task Handle(UserRegisteredEvent domainEvent, CancellationToken cancellationToken)
    {
        logger.LogTrace("Handling User Registered event for {@eventData}", domainEvent.User);

        await SendSystemNotificationEmailAsync(domainEvent, cancellationToken);
    }

    private async Task SendSystemNotificationEmailAsync(UserRegisteredEvent domainEvent, CancellationToken cancellationToken)
    {
        var subject = "New User Registration";
        var userDetails = $"User ID: {domainEvent.User.Id}\n" +
                         $"Email: {domainEvent.User.Email}";
        
        var htmlBody = $"A new user has registered. <br><h2>Details:</h2> {userDetails}";

        var emailModel = new EmailWithBody()
        {
            To = "info@endatix.com",
            From = "noreply@endatix.com",
            Subject = subject,
            HtmlBody = htmlBody,
            PlainTextBody = userDetails
        };

        var validationResult = new EmailWithBodyValidator().Validate(emailModel);
        if (!validationResult.IsValid)
        {
            logger.LogError("Cannot send system notification email as part of {operation} due to invalid data: {errors}", 
                "Send System Notification Email", validationResult.ToString("~"));
            return;
        }
        
        await emailSender.SendEmailAsync(emailModel, cancellationToken);
    }
}
