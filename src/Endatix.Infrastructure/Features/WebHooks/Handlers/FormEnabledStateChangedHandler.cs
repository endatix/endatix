using Endatix.Core.Abstractions;
using Endatix.Core.Events;
using Endatix.Core.Features.WebHooks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.WebHooks.Handlers;

/// <summary>
/// Webhook handler for FormEnabledStateChangedEvent.
/// </summary>
public class FormEnabledStateChangedWebHookHandler(IWebHookService webHookService, ILogger<FormEnabledStateChangedWebHookHandler> logger, IIdGenerator<long> idGenerator) : INotificationHandler<FormEnabledStateChangedEvent>
{
    public async Task Handle(FormEnabledStateChangedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling FormEnabledStateChangedEvent for form {FormId}", notification.Form.Id);

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
            WebHookOperation.FormEnabledStateChanged,
            form);

        await webHookService.EnqueueWebHookAsync(notification.Form.TenantId, message, cancellationToken, notification.Form.Id);
    }
} 