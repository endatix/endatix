using Microsoft.Extensions.Logging;
using Endatix.Core.Events;
using MediatR;

namespace Endatix.Core.Handlers;

/// <summary>
/// Handles the UserLoggedInEvent by logging the event data.
/// </summary>
internal sealed class UserLoggedInEventHandler(ILogger<UserLoggedInEventHandler> logger) : INotificationHandler<UserLoggedInEvent>
{
    public Task Handle(UserLoggedInEvent domainEvent, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("Handling User LoggedIn event for {@EventData}", domainEvent.User);
        }

        return Task.CompletedTask;
    }
}