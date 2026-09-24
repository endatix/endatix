using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Endatix.Core.Features.WebHooks;
using Endatix.Infrastructure.Utils;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// Hands each webhook event to an external delivery worker with one HTTP call and returns: no
/// webhook call and no webhook-config read happen in the relay tick. Any answer other than 200 or
/// 202, a timeout, or an unreachable worker throws, so the relay retries the outbox message.
/// </summary>
internal sealed class WebHookDoorbellOutboxHandler(
    IHttpClientFactory httpClientFactory,
    IOptions<WebHookDoorbellOptions> options,
    ILogger<WebHookDoorbellOutboxHandler> logger) : IOutboxIntegrationEventHandler
{
    public const string HttpClientName = "Endatix.WebHookDoorbell";
    public const string DeliveriesPath = "/webhook-deliveries";
    public const string WorkerKeyHeader = "X-Endatix-Worker-Key";

    private static readonly IReadOnlyCollection<string> _eventTypes =
        new[]
        {
            WebHookOperation.FormCreated,
            WebHookOperation.FormUpdated,
            WebHookOperation.FormEnabledStateChanged,
            WebHookOperation.SubmissionCompleted,
            WebHookOperation.FormDeleted,
        }.Select(operation => StringUtils.ToDottedEventType(operation.EventName)).ToArray();

    /// <inheritdoc />
    public IReadOnlyCollection<string> EventTypes => _eventTypes;

    /// <inheritdoc />
    public async Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken)
    {
        if (!_eventTypes.Contains(message.EventType, StringComparer.Ordinal))
        {
            return;
        }

        // Ambient tenant 0 means "no tenant" off-request; a webhook always belongs to a real tenant.
        if (message.TenantId <= 0)
        {
            throw new InvalidOperationException(
                $"Outbox message {message.Id} ({message.EventType}) has no tenant; webhooks are not delivered for it.");
        }

        using var payload = JsonDocument.Parse(message.Payload);
        var formId = message.GetRequiredIdProp(payload.RootElement, "formId");

        // Ids only: the worker reads the payload and the endpoints itself, at send time.
        var body = JsonSerializer.Serialize(new DeliveryRequest(
            message.TenantId.ToString(CultureInfo.InvariantCulture),
            message.Id.ToString(CultureInfo.InvariantCulture),
            message.EventType,
            formId.ToString(CultureInfo.InvariantCulture)));

        using var request = new HttpRequestMessage(HttpMethod.Post, DeliveriesPath)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(WorkerKeyHeader, options.Value.WorkerApiKey);

        var httpClient = httpClientFactory.CreateClient(HttpClientName);
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException(
                $"Webhook worker did not answer within {options.Value.TimeoutSeconds}s for outbox message {message.Id}.",
                ex);
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Accepted)
            {
                logger.LogDebug(
                    "Webhook worker accepted outbox message {MessageId} ({StatusCode}).",
                    message.Id,
                    (int)response.StatusCode);
                return;
            }

            throw new HttpRequestException(
                $"Webhook worker answered {(int)response.StatusCode} for outbox message {message.Id}.",
                inner: null,
                response.StatusCode);
        }
    }

    private sealed record DeliveryRequest(
        [property: JsonPropertyName("tenantId")] string TenantId,
        [property: JsonPropertyName("outboxMessageId")] string OutboxMessageId,
        [property: JsonPropertyName("eventType")] string EventType,
        [property: JsonPropertyName("formId")] string FormId);
}
