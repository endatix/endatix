using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Features.BackgroundJobs.Handlers;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Outbox.Engine;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// Turns a webhook event into one delivery job per configured endpoint, so each endpoint gets its own retries and
/// a failing one cannot hold up, or re-send to, the others.
/// </summary>
internal sealed class WebHookEndpointExpander(WebHookEventConfigReader configReader)
    : IOutboxSubscriberExpander<WebHookDeliveryPayload>
{
    public async Task<IReadOnlyList<OutboxSubscriber<WebHookDeliveryPayload>>> ExpandAsync(
        IOutboxMessage message,
        long tenantId,
        CancellationToken cancellationToken)
    {
        var endpoints = await EnabledEndpointsAsync(message, tenantId, cancellationToken);
        return
        [
            .. endpoints
                .Where(endpoint => !string.IsNullOrEmpty(endpoint.Url))
                .Select(endpoint => WebHookEndpointKey.Of(endpoint.Url))
                .Distinct(StringComparer.Ordinal)
                .Select(endpointKey => new OutboxSubscriber<WebHookDeliveryPayload>(
                    endpointKey,
                    new WebHookDeliveryPayload(message.Id, endpointKey))),
        ];
    }

    // No endpoints for an event that is not a webhook event, is not configured, or is switched off.
    private async Task<IReadOnlyList<WebHookEndpointConfig>> EnabledEndpointsAsync(
        IOutboxMessage message,
        long tenantId,
        CancellationToken cancellationToken)
    {
        if (!WebHookEvents.OperationsByEventType.TryGetValue(message.EventType, out var operation))
        {
            return [];
        }

        using var document = JsonDocument.Parse(message.Payload);
        var formId = message.GetRequiredIdProp(document.RootElement, "formId");
        var eventConfig = await configReader.GetEventConfigAsync(
            new WebHookEventLookup(tenantId, operation.EventName, formId), cancellationToken);
        return eventConfig is { IsEnabled: true, WebHookEndpoints: { } endpoints } ? [.. endpoints] : [];
    }
}
