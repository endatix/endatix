using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>The input of an outbox work's job: the outbox message whose event it processes.</summary>
/// <remarks>
/// Declare one record per work, e.g. <c>record XPayload(long OutboxMessageId)</c>, with its own
/// <see cref="IBackgroundJobPayload.JobType"/> literal. The job re-reads the message by this id on every attempt.
/// </remarks>
public interface IOutboxJobPayload<TSelf> : IBackgroundJobPayload
    where TSelf : IOutboxJobPayload<TSelf>
{
    /// <summary>The outbox message the job processes.</summary>
    long OutboxMessageId { get; }

    /// <summary>The payload of the job for <paramref name="outboxMessageId"/>.</summary>
    static abstract TSelf For(long outboxMessageId);
}
