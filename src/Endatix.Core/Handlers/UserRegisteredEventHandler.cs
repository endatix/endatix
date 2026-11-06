using Microsoft.Extensions.Logging;
using Endatix.Core.Events;
using MediatR;

namespace Endatix.Core.Handlers;

/// <summary>
/// Default event handler for UserRegisteredEvent.
/// </summary>
internal sealed class UserRegisteredEventHandler(ILogger<UserRegisteredEventHandler> logger) : INotificationHandler<UserRegisteredEvent>
{
    public Task Handle(UserRegisteredEvent domainEvent, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("Handling User Registered event for {@EventData}", domainEvent.User);
        }

        return Task.CompletedTask;
    }
}
