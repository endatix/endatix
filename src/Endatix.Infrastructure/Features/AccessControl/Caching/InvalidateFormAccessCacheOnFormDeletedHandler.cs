using Endatix.Core.Events;
using Endatix.Infrastructure.Caching;
using MediatR;

namespace Endatix.Infrastructure.Features.AccessControl.Caching;

internal sealed class InvalidateFormAccessCacheOnFormDeletedHandler(
    IFormAccessCacheInvalidator cacheInvalidator) : INotificationHandler<FormDeletedEvent>
{
    public Task Handle(FormDeletedEvent notification, CancellationToken cancellationToken) =>
        cacheInvalidator.InvalidateFormAsync(notification.Form.Id, cancellationToken);
}
