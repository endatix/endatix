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
            WebHookOperation.FormDeleted,
            form);

        await webHookService.EnqueueWebHookAsync(message, cancellationToken);
    }
} 