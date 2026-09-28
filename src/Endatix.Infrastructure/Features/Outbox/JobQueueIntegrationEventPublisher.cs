using System.Diagnostics;
using System.Diagnostics.Metrics;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// Delivers an outbox message to the job queue: one job per subscriber, enqueued in one batch, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// The relay's work per message shrinks to one enqueue, so a slow or failing subscriber can no longer hold up,
/// re-send or dead-letter another: each subscriber's job has its own retries and status. It makes no HTTP call
/// and writes nothing but job rows.
/// </para>
/// <para>
/// A crash between the enqueue and the relay marking the message sent redelivers the message; every job carries
/// the dedup key <c>{messageId}:{subscriberKey}</c>, so the redelivery enqueues nothing new. The fan-out lives
/// here because a job queue has no topics; a broker publisher that replaces this one does it through its own
/// subscriptions.
/// </para>
/// </remarks>
internal sealed class JobQueueIntegrationEventPublisher : IIntegrationEventPublisher
{
    private static readonly ActivitySource _activitySource = new(OutboxTelemetry.SourceName);

    private readonly IServiceProvider _services;
    private readonly OutboxSubscriptions _subscriptions;
    private readonly ILogger<JobQueueIntegrationEventPublisher> _logger;
    private readonly Counter<long> _fannedOutJobs;

    public JobQueueIntegrationEventPublisher(
        IServiceProvider services,
        OutboxSubscriptions subscriptions,
        IMeterFactory meterFactory,
        ILogger<JobQueueIntegrationEventPublisher> logger)
    {
        _services = services;
        _subscriptions = subscriptions;
        _logger = logger;
        _fannedOutJobs = meterFactory.Create(OutboxTelemetry.SourceName).CreateCounter<long>(
            "endatix.outbox.fanout.jobs",
            unit: "{job}",
            description: "Jobs the outbox relay enqueued for its subscribers.");
    }

    /// <inheritdoc />
    public async Task PublishAsync(IOutboxMessage message, CancellationToken cancellationToken)
    {
        // The jobs capture the current trace at enqueue, so running under the message's trace carries the
        // originating request through the relay and into every subscriber's job.
        using var activity = _activitySource.StartActivity(
            "outbox.fan_out",
            ActivityKind.Consumer,
            ParentOf(message),
            tags: [new("endatix.outbox.message_id", message.Id), new("endatix.outbox.event_type", message.EventType)]);

        var subscriptions = _subscriptions.For(message.EventType);
        if (subscriptions.Count == 0)
        {
            _logger.LogDebug(
                "Outbox message {MessageId} has no subscriber for event type '{EventType}'.",
                message.Id,
                message.EventType);
            return;
        }

        List<BackgroundJobRequest> requests = [];
        foreach (var subscription in subscriptions)
        {
            var tenantId = subscription.ResolveTenantId(message);

            // A job without a tenant would be visible to no tenant and run as all of them. Failing the publish
            // retries the message and then fails it, where an operator sees it, instead of writing such a row.
            if (tenantId <= 0)
            {
                throw new InvalidOperationException(
                    $"Outbox message {message.Id} ({message.EventType}) resolved no tenant for subscriber {subscription.JobType}.");
            }

            var subscribers = await subscription.BuildRequestsAsync(_services, message, tenantId, cancellationToken);
            requests.AddRange(subscribers.Select(subscriber => subscriber.Request));
        }

        if (requests.Count == 0)
        {
            return;
        }

        var queue = _services.GetRequiredService<IBackgroundJobQueue>();
        await queue.EnqueueManyAsync(requests, cancellationToken);

        _fannedOutJobs.Add(requests.Count, new KeyValuePair<string, object?>("event_type", message.EventType));
    }

    // The outbox stores the bare trace id, so the parent is that trace with a span of its own.
    private static ActivityContext ParentOf(IOutboxMessage message)
    {
        if (message.TraceId is not { Length: 32 } traceId)
        {
            return default;
        }

        try
        {
            return new ActivityContext(
                ActivityTraceId.CreateFromString(traceId),
                ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.Recorded,
                isRemote: true);
        }
        catch (ArgumentOutOfRangeException)
        {
            return default;
        }
    }
}
