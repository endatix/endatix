using Endatix.Core.Abstractions;
using Endatix.Core.Events;
using Endatix.Core.Features.WebHooks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.WebHooks.Handlers;

/// <summary>
/// Webhook handler for FormDeletedEvent.
/// </summary>
public class FormDeletedHandler(IWebHookService webHookService, ILogger<FormDeletedHandler> logger, IIdGenerator<long> idGenerator) : INotificationHandler<FormDeletedEvent>
{
    public async Task Handle(FormDeletedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling FormDeletedEvent for form {FormId}", notification.Form.Id);

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
            WebHookOperation.FormDeleted,
            form);

        await webHookService.EnqueueWebHookAsync(notification.Form.TenantId, message, cancellationToken, notification.Form.Id);
    }
} 