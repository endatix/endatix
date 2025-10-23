using Endatix.Core.Events;
using Endatix.Core.Features.WebHooks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.WebHooks.Handlers;

/// <summary>
/// Webhook handler for FormDeletedEvent.
/// </summary>
public class FormDeletedHandler(IWebHookService webHookService, ILogger<FormDeletedHandler> logger) : INotificationHandler<FormDeletedEvent>
{
    public async Task Handle(FormDeletedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling FormDeletedEvent for form {FormId}", notification.Form.Id);

        var form = new
        {
            id = notification.Form.Id,
            tenantId = notification.Form.TenantId,
            name = notification.Form.Name,
            description = notification.Form.Description,
            isEnabled = notification.Form.IsEnabled,
            activeDefinitionId = notification.Form.ActiveDefinitionId,
            themeId = notification.Form.ThemeId,
            createdAt = notification.Form.CreatedAt,
            modifiedAt = notification.Form.ModifiedAt,
        };

        var message = new WebHookMessage<object>(
            notification.Form.Id,
            WebHookOperation.FormDeleted,
            form);

        await webHookService.EnqueueWebHookAsync(notification.Form.TenantId, message, cancellationToken);
    }
} 