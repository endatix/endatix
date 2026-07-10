using System.Text.Json;
using Endatix.Core.Features.WebHooks;
using Endatix.Infrastructure.Utils;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// Delivers mapped integration events to configured webhook endpoints via <see cref="IWebHookService"/>.
/// </summary>
internal sealed class WebHookOutboxIntegrationEventHandler(
    IWebHookService webHookService,
    ILogger<WebHookOutboxIntegrationEventHandler> logger) : IOutboxIntegrationEventHandler
{
    private static readonly IReadOnlyDictionary<string, WebHookOperation> _operationsByEventType =
        new[]
        {
            WebHookOperation.FormCreated,
            WebHookOperation.FormUpdated,
            WebHookOperation.FormEnabledStateChanged,
            WebHookOperation.SubmissionCompleted,
            WebHookOperation.FormDeleted,
        }.ToDictionary(operation => StringUtils.ToDottedEventType(operation.EventName), StringComparer.Ordinal);

    private static readonly IReadOnlyCollection<string> _handledEventTypes =
        _operationsByEventType.Keys.ToArray();

    /// <inheritdoc />
    public IReadOnlyCollection<string> EventTypes => _handledEventTypes;

    /// <inheritdoc />
    public async Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken)
    {
        if (!_operationsByEventType.TryGetValue(message.EventType, out var operation))
        {
            return;
        }

        using var document = JsonDocument.Parse(message.Payload);
        var payload = document.RootElement;

        var formId = message.GetRequiredIdProp(payload, "formId");

        WebHookMessage<JsonElement> webHookMessage = new(message.Id, operation, payload);
        var delivered = await webHookService.DeliverWebHookAsync(
            message.TenantId,
            webHookMessage,
            cancellationToken,
            formId);

        if (!delivered)
        {
            throw new InvalidOperationException(
                $"Webhook delivery failed for outbox message {message.Id} ({message.EventType}); it will be retried.");
        }

        logger.LogDebug(
            "Delivered webhook for outbox message {MessageId} ({EventType}).",
            message.Id,
            message.EventType);
    }
}
