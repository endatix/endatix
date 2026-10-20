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
    internal OutboxJobSubscription()
    {
    }

    /// <summary>The outbox event type the subscription listens to.</summary>
    public string EventType { get; internal init; } = string.Empty;

    /// <summary>The job type each of its jobs runs as.</summary>
    public string JobType { get; internal init; } = string.Empty;

    /// <summary>The assembly that owns the work — the payload's.</summary>
    public Assembly SourceAssembly { get; internal init; } = null!;

    /// <summary>The <see cref="IOutboxWork"/> whose job this is, when the work registered it; otherwise none.</summary>
    internal Type? WorkType { get; init; }

    /// <summary>
    /// Whether this subscription does <paramref name="handler"/>'s work as a job: an <see cref="IOutboxWork"/> needs its
    /// own job, any other handler one registered from its assembly that no work owns.
    /// </summary>
    internal bool Covers(IOutboxIntegrationEventHandler handler) =>
        handler is IOutboxWork
            ? WorkType == handler.GetType()
            : WorkType is null && SourceAssembly == handler.GetType().Assembly;

    /// <summary>Reads the tenant from an app-level message; <see langword="null"/> takes the message's own.</summary>
    internal Func<IOutboxMessage, long>? TenantResolver { private get; init; }

    /// <summary>Builds the job requests for a message delivered to one tenant.</summary>
    internal OutboxRequestsBuilder Requests { private get; init; } = null!;

    /// <summary>
    /// The tenant the jobs belong to: the message's own, or what the subscription's resolver reads from the
    /// message when the message is app-level.
    /// </summary>
    public long ResolveTenantId(IOutboxMessage message) =>
        TenantResolver is null ? message.TenantId : TenantResolver(message);

    /// <summary>The job requests for <paramref name="delivery"/>, one per subscriber.</summary>
    internal Task<IReadOnlyList<BackgroundJobRequest>> BuildRequestsAsync(
        IServiceProvider services,
        OutboxDelivery delivery,
        CancellationToken cancellationToken) =>
        Requests(services, delivery, cancellationToken);
}

/// <summary>Builds the job requests one subscription makes of a message delivered to one tenant.</summary>
internal delegate Task<IReadOnlyList<BackgroundJobRequest>> OutboxRequestsBuilder(
    IServiceProvider services,
    OutboxDelivery delivery,
    CancellationToken cancellationToken);

/// <summary>An outbox message, and the tenant whose jobs it becomes.</summary>
internal readonly record struct OutboxDelivery(IOutboxMessage Message, long TenantId);

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
        var handlers = inlineHandlers.ToList();
        ThrowIfRegisteredTwice(handlers);
        foreach (var handler in handlers)
        {
            ThrowIfJobRunsOnOtherEvents(handler);
            var eventType = handler.EventTypes.FirstOrDefault(type =>
                !_byEventType[type].Any(subscription => subscription.Covers(handler)));
            if (eventType is not null)
            {
                throw new InvalidOperationException(
                    $"The outbox handler {handler.GetType().FullName} handles '{eventType}', but its module registers " +
                    $"no job subscription for it, so with Endatix:Outbox:DeliverToJobQueue on that work would be " +
                    $"marked sent and never done. Register the subscription or turn the switch off.");
            }
        }
    }

    // However it was registered, an inline handler type that resolves twice runs every message twice.
    private static void ThrowIfRegisteredTwice(List<IOutboxIntegrationEventHandler> handlers)
    {
        var twice = handlers.GroupBy(handler => handler.GetType()).FirstOrDefault(group => group.Count() > 1);
        if (twice is not null)
        {
            throw new InvalidOperationException(
                $"The outbox handler {twice.Key.FullName} is registered {twice.Count()} times, so it would run every " +
                $"message that often inline. Register it once.");
        }
    }

    // A work's job subscribes HandledEventTypes, its inline handler runs on EventTypes: they must be the same events.
    private void ThrowIfJobRunsOnOtherEvents(IOutboxIntegrationEventHandler handler)
    {
        var extra = _byEventType.SelectMany(group => group).FirstOrDefault(subscription =>
            subscription.WorkType == handler.GetType() && !handler.EventTypes.Contains(subscription.EventType));
        if (extra is not null)
        {
            throw new InvalidOperationException(
                $"The outbox work {handler.GetType().FullName} runs its job on '{extra.EventType}', which its EventTypes " +
                $"do not list, so inline and job delivery would do different work. Make EventTypes return HandledEventTypes.");
        }
    }
}
