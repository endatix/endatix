using Endatix.Core.Abstractions;
using Endatix.Core.Events;
using Endatix.Core.Features.WebHooks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.WebHooks.Handlers;

/// <summary>
/// Webhook handler for FormUpdatedEvent.
/// </summary>
public class FormUpdatedWebHookHandler(IWebHookService webHookService, ILogger<FormUpdatedWebHookHandler> logger, IIdGenerator<long> idGenerator) : INotificationHandler<FormUpdatedEvent>
{
    public async Task Handle(FormUpdatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling FormUpdatedEvent for form {FormId}", notification.Form.Id);

        var form = new
        {
            formId = notification.Form.Id,
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
            idGenerator.CreateId(),
            WebHookOperation.FormUpdated,
            form);

        await webHookService.EnqueueWebHookAsync(notification.Form.TenantId, message, cancellationToken, notification.Form.Id);
    }
} 