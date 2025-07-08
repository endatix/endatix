using Endatix.Core.Events;
using Endatix.Core.Features.WebHooks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.WebHooks.Handlers;

/// <summary>
/// Webhook handler for FormEnabledStateChangedEvent.
/// </summary>
public class FormEnabledStateChangedWebHookHandler(IWebHookService webHookService, ILogger<FormEnabledStateChangedWebHookHandler> logger) : INotificationHandler<FormEnabledStateChangedEvent>
{
    public async Task Handle(FormEnabledStateChangedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling FormEnabledStateChangedEvent for form {FormId}", notification.Form.Id);

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
            WebHookOperation.FormEnabledStateChanged,
            form);

        await webHookService.EnqueueWebHookAsync(message, cancellationToken);
    }
} 