using Endatix.Core.Events;
using Endatix.Core.Features.WebHooks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.WebHooks.Handlers;

/// <summary>
/// Webhook handler for FormUpdatedEvent.
/// </summary>
public class FormUpdatedWebHookHandler(IWebHookService webHookService, ILogger<FormUpdatedWebHookHandler> logger) : INotificationHandler<FormUpdatedEvent>
{
    public async Task Handle(FormUpdatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling FormUpdatedEvent for form {FormId}", notification.Form.Id);

        var form = new
        {
            notification.Form.Id,
            notification.Form.TenantId,
            notification.Form.Name,
            notification.Form.Description,
            notification.Form.IsEnabled,
            notification.Form.ActiveDefinitionId,
            notification.Form.ThemeId,
            notification.Form.CreatedAt,
            notification.Form.ModifiedAt,
        };

        var message = new WebHookMessage<object>(
            notification.Form.Id,
            WebHookOperation.FormUpdated,
            form);

        await webHookService.EnqueueWebHookAsync(message, cancellationToken);
    }
} 