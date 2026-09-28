using Ardalis.GuardClauses;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// Registers outbox subscriptions: the jobs an outbox event becomes when the relay delivers to the job queue.
/// </summary>
/// <remarks>
/// A module registers its own subscriptions in its <c>ConfigureServices</c>, next to its job handlers. Every host
/// that runs the relay must register every subscribing module, with the same module flags as the rest of the
/// deployment: a relay host without a module marks that module's events sent without enqueuing its jobs.
/// </remarks>
public static class OutboxSubscriptionServiceCollectionExtensions
{
    /// <summary>
    /// Subscribes one job of <typeparamref name="TPayload"/>'s job type to <paramref name="eventType"/>: each
    /// message becomes one job, identified among the message's subscribers by that job type.
    /// </summary>
    /// <param name="resolveTenantId">
    /// Reads the tenant the job belongs to from the message. Defaults to the message's own tenant; an app-level
    /// message has none, so its subscription must say whose work it is.
    /// </param>
    public static IServiceCollection AddOutboxJobSubscription<TPayload>(
        this IServiceCollection services,
        string eventType,
        Func<IOutboxMessage, TPayload> createPayload,
        Func<IOutboxMessage, long>? resolveTenantId = null)
        where TPayload : IBackgroundJobPayload
    {
        Guard.Against.NullOrWhiteSpace(eventType);
        Guard.Against.Null(createPayload);

        return services.AddSubscription(new OutboxJobSubscription(
            eventType,
            TPayload.JobType,
            typeof(TPayload).Assembly,
            resolveTenantId,
            (_, message, tenantId, _) => Task.FromResult<IReadOnlyList<(string, BackgroundJobRequest)>>(
            [
                (TPayload.JobType, Request(message, TPayload.JobType, createPayload(message), tenantId)),
            ])));
    }

    /// <summary>
    /// Subscribes jobs of <typeparamref name="TPayload"/>'s job type to <paramref name="eventType"/>, as many per
    /// message as <typeparamref name="TExpander"/> finds subscribers.
    /// </summary>
    public static IServiceCollection AddOutboxJobSubscription<TPayload, TExpander>(
        this IServiceCollection services,
        string eventType,
        Func<IOutboxMessage, long>? resolveTenantId = null)
        where TPayload : IBackgroundJobPayload
        where TExpander : class, IOutboxSubscriberExpander<TPayload>
    {
        Guard.Against.NullOrWhiteSpace(eventType);

        services.TryAddScoped<TExpander>();

        return services.AddSubscription(new OutboxJobSubscription(
            eventType,
            TPayload.JobType,
            typeof(TPayload).Assembly,
            resolveTenantId,
            async (provider, message, tenantId, cancellationToken) =>
            {
                var subscribers = await provider.GetRequiredService<TExpander>()
                    .ExpandAsync(message, tenantId, cancellationToken);
                return
                [
                    .. subscribers.Select(subscriber => (
                        subscriber.SubscriberKey,
                        Request(message, subscriber.SubscriberKey, subscriber.Payload, tenantId))),
                ];
            }));
    }

    /// <summary>
    /// The dedup key of a subscriber's job for a message: the same across redeliveries of the message and
    /// distinct per subscriber, so a redelivery enqueues nothing new.
    /// </summary>
    public static string DedupKeyFor(IOutboxMessage message, string subscriberKey) => $"{message.Id}:{subscriberKey}";

    private static BackgroundJobRequest Request<TPayload>(
        IOutboxMessage message,
        string subscriberKey,
        TPayload payload,
        long tenantId)
        where TPayload : IBackgroundJobPayload =>
        BackgroundJobRequest.Create(payload, tenantId, dedupKey: DedupKeyFor(message, subscriberKey));

    private static IServiceCollection AddSubscription(this IServiceCollection services, OutboxJobSubscription subscription)
    {
        services.AddSingleton(subscription);
        services.TryAddSingleton<OutboxSubscriptions>();
        return services;
    }
}
