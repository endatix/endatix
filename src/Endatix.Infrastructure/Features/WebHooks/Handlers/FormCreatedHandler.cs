using Endatix.Core.Events;
using Endatix.Core.Features.WebHooks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.WebHooks.Handlers;

/// <summary>
/// Webhook handler for FormCreatedEvent.
/// </summary>
public class FormCreatedHandler(IWebHookService webHookService, ILogger<FormCreatedHandler> logger) : INotificationHandler<FormCreatedEvent>
{
    public async Task Handle(FormCreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling FormCreatedEvent for form {FormId}", notification.Form.Id);

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
            WebHookOperation.FormCreated,
            form);

        await webHookService.EnqueueWebHookAsync(message, cancellationToken);
    }
} 