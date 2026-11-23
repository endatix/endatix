using Endatix.Core.Events;
using Endatix.Infrastructure.Caching;
using MediatR;

namespace Endatix.Infrastructure.Features.AccessControl.Caching;

internal sealed class InvalidateFormAccessCacheOnFormDefinitionChangedHandler(
    IFormAccessCacheInvalidator cacheInvalidator) : INotificationHandler<FormDefinitionChangedEvent>
{
    public Task Handle(FormDefinitionChangedEvent notification, CancellationToken cancellationToken) =>
        cacheInvalidator.InvalidateFormAsync(notification.FormId, cancellationToken);
}
