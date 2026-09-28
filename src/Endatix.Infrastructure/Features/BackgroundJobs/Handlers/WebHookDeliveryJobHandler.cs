using System.Text.Json;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Entities;
using Endatix.Core.Features.WebHooks;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Features.WebHooks;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.BackgroundJobs.Handlers;

/// <summary>
/// Delivers one outbox event to one webhook endpoint, with one POST per attempt.
/// </summary>
/// <remarks>
/// <para>
/// Everything is read when the job runs: the event from its outbox row, scoped to the job's tenant, and the
/// endpoint from the current configuration, so a job that waited out a backoff sends to the endpoint as it is
/// now configured and never carries its URL or credentials.
/// </para>
/// <para>
/// A non-2xx answer or a failed request throws, so the job retries on its own policy; the HTTP client here does
/// not retry, which makes the job's attempt budget the number of POSTs an endpoint receives. A message, endpoint
/// or event configuration that is gone returns a failure: no retry can bring it back. Receivers deduplicate on
/// <c>X-Endatix-Hook-Id</c>, the outbox message id, exactly as with delivery inside the relay.
/// </para>
/// </remarks>
internal sealed class WebHookDeliveryJobHandler(
    IRepository<OutboxMessage> outboxMessages,
    WebHookEventConfigReader configReader,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory,
    ILogger<WebHookDeliveryJobHandler> logger) : BackgroundJobHandler<WebHookDeliveryPayload>(logger)
{
    /// <summary>The HTTP client webhook jobs send with: the webhook client's settings, without its retries.</summary>
    public const string HttpClientName = "webhook-job-delivery";

    public const string MessageGone = "The outbox message no longer exists.";
    public const string EndpointGone = "The webhook endpoint is no longer configured.";
    public const string EventDisabled = "Webhook delivery for this event is disabled.";
    public const string InvalidUrl = "The webhook endpoint URL is not valid.";

    protected override async Task<Result> ExecuteAsync(
        BackgroundJobContext job,
        WebHookDeliveryPayload payload,
        CancellationToken cancellationToken)
    {
        var message = await ReadMessageAsync(job, payload, cancellationToken);
        if (message is null || !WebHookEvents.OperationsByEventType.TryGetValue(message.EventType, out var operation))
        {
            return Refused(MessageGone);
        }

        using var document = JsonDocument.Parse(message.Payload);
        var delivery = new EndpointDelivery(message, operation, document.RootElement, payload.EndpointKey);
        var endpoint = await FindEndpointAsync(job.TenantId, delivery, cancellationToken);
        return endpoint.Refusal is { } refusal
            ? Refused(refusal)
            : await SendAsync(delivery, endpoint.Endpoint!, cancellationToken);
    }

    // Scoped to the job's tenant explicitly: outside a request nothing else scopes this read.
    private Task<OutboxMessage?> ReadMessageAsync(
        BackgroundJobContext job,
        WebHookDeliveryPayload payload,
        CancellationToken cancellationToken) =>
        outboxMessages.FirstOrDefaultAsync(
            new OutboxMessageByIdForTenantSpec(payload.OutboxMessageId, job.TenantId),
            cancellationToken);

    private static Result Refused(string reason) => Result.Invalid(new ValidationError(reason));

    // The endpoint as it is configured now, or why the event can no longer be sent to it.
    private async Task<EndpointLookup> FindEndpointAsync(
        long tenantId,
        EndpointDelivery delivery,
        CancellationToken cancellationToken)
    {
        var formId = new OutboxMessageView(delivery.Message).GetRequiredIdProp(delivery.EventPayload, "formId");
        var eventConfig = await configReader.GetEventConfigAsync(
            new WebHookEventLookup(tenantId, delivery.Operation.EventName, formId), cancellationToken);
        return eventConfig is { IsEnabled: true }
            ? LookUpIn(eventConfig, delivery.EndpointKey)
            : EndpointLookup.NoneBecause(EventDisabled);
    }

    private static EndpointLookup LookUpIn(WebHookEventConfig eventConfig, string endpointKey)
    {
        var endpoint = eventConfig.WebHookEndpoints?
            .FirstOrDefault(candidate => !string.IsNullOrEmpty(candidate.Url)
                && WebHookEndpointKey.Of(candidate.Url) == endpointKey);
        return endpoint switch
        {
            null => EndpointLookup.NoneBecause(EndpointGone),
            _ when !Uri.IsWellFormedUriString(endpoint.Url, UriKind.Absolute) => EndpointLookup.NoneBecause(InvalidUrl),
            _ => new EndpointLookup(endpoint, null),
        };
    }

    // One POST, through the client without retries; any answer but success is thrown for the job to retry.
    private async Task<Result> SendAsync(
        EndpointDelivery delivery,
        WebHookEndpointConfig endpoint,
        CancellationToken cancellationToken)
    {
        var server = new WebHookServer(
            httpClientFactory.CreateClient(HttpClientName), loggerFactory.CreateLogger<WebHookServer>());
        var status = await server.SendAsync(
            new WebHookMessage<JsonElement>(delivery.Message.Id, delivery.Operation, delivery.EventPayload),
            TaskInstructions.FromWebHookEndpointConfig(endpoint),
            cancellationToken);

        if ((int)status is < 200 or > 299)
        {
            throw new WebHookDeliveryFailedException(delivery.EndpointKey, status);
        }

        return Result.Success();
    }

    /// <summary>The event a job delivers, and the key of the endpoint it delivers to.</summary>
    private sealed record EndpointDelivery(
        OutboxMessage Message,
        WebHookOperation Operation,
        JsonElement EventPayload,
        string EndpointKey);

    /// <summary>The endpoint to send to, or why there is none.</summary>
    private sealed record EndpointLookup(WebHookEndpointConfig? Endpoint, string? Refusal)
    {
        public static EndpointLookup NoneBecause(string refusal) => new(null, refusal);
    }
}

/// <summary>
/// A webhook endpoint answered something other than success. Retryable: the endpoint may recover.
/// </summary>
/// <remarks>Names the endpoint by its key, never its URL, which may carry a secret.</remarks>
public sealed class WebHookDeliveryFailedException(string endpointKey, System.Net.HttpStatusCode status)
    : Exception($"Webhook endpoint {endpointKey} answered {(int)status}.")
{
    public string EndpointKey { get; } = endpointKey;

    public System.Net.HttpStatusCode Status { get; } = status;
}
