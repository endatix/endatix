using Microsoft.Extensions.Logging;
using Endatix.Core.Events;
using MediatR;

namespace Endatix.Core.Handlers;

/// <summary>
/// Default event handler for UserLoggedOutEvent.
/// </summary>
internal sealed class UserLoggedOutEventHandler(ILogger<UserLoggedOutEventHandler> logger) : INotificationHandler<UserLoggedOutEvent>
{
    public Task Handle(UserLoggedOutEvent domainEvent, CancellationToken cancellationToken)
    {
        logger.LogUserLoggedOutHandled(domainEvent.User);

        return Task.CompletedTask;
    }
}