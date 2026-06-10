using Endatix.Core.Events;
using Endatix.Infrastructure.Caching;
using MediatR;

namespace Endatix.Infrastructure.Features.AccessControl.Caching;

internal sealed class InvalidateFormAccessCacheOnFormUpdatedHandler(
    IFormAccessCacheInvalidator cacheInvalidator) : INotificationHandler<FormUpdatedEvent>
{
    public Task Handle(FormUpdatedEvent notification, CancellationToken cancellationToken) =>
        cacheInvalidator.InvalidateFormAsync(notification.Form.Id, cancellationToken);
}
