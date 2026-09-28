using System.Reflection;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Outbox.Engine;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// One subscription: an outbox event type bound to the job type that does a module's work for it.
/// </summary>
/// <remarks>
/// Built by <see cref="OutboxSubscriptionServiceCollectionExtensions"/>; a module registers its own, next to its
/// job handlers.
/// </remarks>
public sealed class OutboxJobSubscription
{
    private readonly Func<IServiceProvider, IOutboxMessage, long, CancellationToken, Task<IReadOnlyList<(string SubscriberKey, BackgroundJobRequest Request)>>> _requests;
    private readonly Func<IOutboxMessage, long>? _resolveTenantId;

    internal OutboxJobSubscription(
        string eventType,
        string jobType,
        Assembly sourceAssembly,
        Func<IOutboxMessage, long>? resolveTenantId,
        Func<IServiceProvider, IOutboxMessage, long, CancellationToken, Task<IReadOnlyList<(string SubscriberKey, BackgroundJobRequest Request)>>> requests)
    {
        EventType = eventType;
        JobType = jobType;
        SourceAssembly = sourceAssembly;
        _resolveTenantId = resolveTenantId;
        _requests = requests;
    }

    /// <summary>The outbox event type the subscription listens to.</summary>
    public string EventType { get; }

    /// <summary>The job type each of its jobs runs as.</summary>
    public string JobType { get; }

    /// <summary>The assembly that owns the work — the payload's.</summary>
    public Assembly SourceAssembly { get; }

    /// <summary>
    /// The tenant the jobs belong to: the message's own, or what the subscription's resolver reads from the
    /// message when the message is app-level.
    /// </summary>
    public long ResolveTenantId(IOutboxMessage message) =>
        _resolveTenantId is null ? message.TenantId : _resolveTenantId(message);

    /// <summary>The job requests for <paramref name="message"/>, each with its subscriber key.</summary>
    internal Task<IReadOnlyList<(string SubscriberKey, BackgroundJobRequest Request)>> BuildRequestsAsync(
        IServiceProvider services,
        IOutboxMessage message,
        long tenantId,
        CancellationToken cancellationToken) =>
        _requests(services, message, tenantId, cancellationToken);
}

/// <summary>
/// Every outbox subscription registered on this host. Only the job-queue publisher reads it; the Jobs module has
/// its own registry, of job types to handlers.
/// </summary>
public sealed class OutboxSubscriptions(IEnumerable<OutboxJobSubscription> subscriptions)
{
    private readonly ILookup<string, OutboxJobSubscription> _byEventType =
        subscriptions.ToLookup(subscription => subscription.EventType, StringComparer.Ordinal);

    /// <summary>The subscriptions to <paramref name="eventType"/>, in registration order.</summary>
    public IReadOnlyList<OutboxJobSubscription> For(string eventType) => [.. _byEventType[eventType]];

    /// <summary>
    /// Refuses to deliver to the job queue while any inline handler would lose its events: every event type an
    /// inline handler lists needs a subscription registered from the handler's own assembly, or the relay would
    /// mark the event sent with that module's work never done. A module that is not loaded at all cannot be seen
    /// here; that stays a deployment rule.
    /// </summary>
    public void Validate(IEnumerable<IOutboxIntegrationEventHandler> inlineHandlers)
    {
        foreach (var handler in inlineHandlers)
        {
            var handlerAssembly = handler.GetType().Assembly;
            foreach (var eventType in handler.EventTypes)
            {
                if (!_byEventType[eventType].Any(subscription => subscription.SourceAssembly == handlerAssembly))
                {
                    throw new InvalidOperationException(
                        $"The outbox handler {handler.GetType().FullName} handles '{eventType}', but its module registers " +
                        $"no job subscription for it, so with Endatix:Outbox:DeliverToJobQueue on that work would be " +
                        $"marked sent and never done. Register the subscription or turn the switch off.");
                }
            }
        }
    }
}
