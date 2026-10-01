using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Outbox.Engine;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// Turns one outbox message into the subscribers of a subscription that fans out to several jobs — one per
/// webhook endpoint, for instance. Resolved from the relay tick's scope.
/// </summary>
/// <typeparam name="TPayload">The payload of each subscriber's job.</typeparam>
public interface IOutboxSubscriberExpander<TPayload>
    where TPayload : IBackgroundJobPayload
{
    /// <summary>
    /// The subscribers of <paramref name="message"/>, each with the key that identifies it among them and the
    /// payload of its job. The key must be stable across redeliveries of the same message, because it is part of
    /// the job's dedup key. An empty list enqueues nothing.
    /// </summary>
    Task<IReadOnlyList<OutboxSubscriber<TPayload>>> ExpandAsync(
        IOutboxMessage message,
        long tenantId,
        CancellationToken cancellationToken);
}

/// <param name="SubscriberKey">Identifies this subscriber among the message's subscribers.</param>
/// <param name="Payload">The input of this subscriber's job.</param>
public sealed record OutboxSubscriber<TPayload>(string SubscriberKey, TPayload Payload)
    where TPayload : IBackgroundJobPayload;
