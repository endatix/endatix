using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Infrastructure.Features.BackgroundJobs;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// Registers a module's outbox work: the inline handler the relay runs, the same work as a background job, and the
/// subscriptions that enqueue it. Which one delivers is the relay's switch (<c>Endatix:Outbox:DeliverToJobQueue</c>).
/// </summary>
public static class OutboxWorkServiceCollectionExtensions
{
    /// <summary>The retry policy of one short outbox update, when a call passes no <paramref name="defaults"/>.</summary>
    public static BackgroundJobTypeDefaults DefaultJob { get; } = new()
    {
        MaxAttempts = 5,
        MaxRuntimeMinutes = 10,
        BackoffBaseSeconds = 10,
        BackoffCapSeconds = 600,
        RetentionDays = 3,
        MaxConcurrency = 2,
    };

    /// <summary>
    /// Registers <typeparamref name="TWork"/> as an inline handler, its job keyed by <typeparamref name="TWork"/>'s
    /// <see cref="IOutboxWork{TSelf}.JobType"/> with <paramref name="defaults"/>, and a job subscription for every
    /// event type in <see cref="IOutboxWork{TSelf}.HandledEventTypes"/>.
    /// </summary>
    /// <param name="defaults">The job's retry policy. Omitted uses <see cref="DefaultJob"/>.</param>
    /// <param name="appLevelTenant">
    /// Reads the job's tenant from an app-level row, whose stored tenant is none. Omitted, the row carries the tenant.
    /// </param>
    /// <param name="onlyWhen">
    /// Enqueues no job when this returns false, so a frequent event that rarely needs the work adds no empty jobs.
    /// A throw other than cancellation still enqueues the job. The relay calls this static predicate and does not
    /// resolve <typeparamref name="TWork"/>.
    /// </param>
    public static IServiceCollection AddOutboxWork<TWork>(
        this IServiceCollection services,
        BackgroundJobTypeDefaults? defaults = null,
        Func<IOutboxMessage, long>? appLevelTenant = null,
        Func<IOutboxMessage, bool>? onlyWhen = null)
        where TWork : class, IOutboxWork<TWork>
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(RegistersInline<TWork>))
        {
            throw new InvalidOperationException(
                $"The outbox handler {typeof(TWork).FullName} is already registered; register it once, so it runs once.");
        }

        // Registered as itself too, so a job in the same scope reuses the very same work.
        services.AddScoped<TWork>();
        services.AddScoped<IOutboxIntegrationEventHandler>(provider => provider.GetRequiredService<TWork>());
        services.AddBackgroundJobHandler<OutboxJobHandler<TWork>, OutboxMessageJob<TWork>>(defaults ?? DefaultJob);
        services.TryAddScoped<OutboxJobMessages>();
        services.AddSingleton(new OutboxWorkRegistration<TWork>(appLevelTenant, onlyWhen));

        foreach (var eventType in TWork.HandledEventTypes)
        {
            var source = new OutboxSubscriptionServiceCollectionExtensions.SubscriptionSource(
                eventType,
                appLevelTenant is { } readTenant ? TenantOrNone(readTenant) : null,
                typeof(TWork));
            if (onlyWhen is null)
            {
                services.AddJobSubscription(source, message => new OutboxMessageJob<TWork>(message.Id));
            }
            else
            {
                services.AddExpandingJobSubscription<OutboxMessageJob<TWork>, OutboxWorkExpander<TWork>>(source);
            }
        }

        return services;
    }

    private static bool RegistersInline<TWork>(ServiceDescriptor descriptor) =>
        descriptor.ServiceType == typeof(TWork)
        || (descriptor.ServiceType == typeof(IOutboxIntegrationEventHandler)
            && !descriptor.IsKeyedService
            && descriptor.ImplementationType == typeof(TWork));

    // An unreadable tenant resolves to none, which fails the publish, so the relay retries and then fails the message
    // rather than writing a job without a tenant.
    private static Func<IOutboxMessage, long> TenantOrNone(Func<IOutboxMessage, long> readTenant) => message =>
    {
        try
        {
            return readTenant(message);
        }
        catch (Exception exception) when (OutboxWorkReader.IsUnreadable(exception))
        {
            return 0;
        }
    };
}

/// <summary>The input of an outbox work's job: the outbox message whose event it processes.</summary>
internal sealed record OutboxMessageJob<TWork>(long OutboxMessageId) : IBackgroundJobPayload
    where TWork : class, IOutboxWork<TWork>
{
    public static string JobType => TWork.JobType;
}

/// <summary>What one work's registration told its job and its relay filter.</summary>
internal sealed class OutboxWorkRegistration<TWork>(
    Func<IOutboxMessage, long>? readTenant,
    Func<IOutboxMessage, bool>? onlyWhen)
    where TWork : class, IOutboxWork<TWork>
{
    public Func<IOutboxMessage, long>? ReadTenant { get; } = readTenant;

    public Func<IOutboxMessage, bool>? OnlyWhen { get; } = onlyWhen;

    public string JobType => TWork.JobType;
}

/// <summary>Subscribes the job of <typeparamref name="TWork"/> only for a message that may need its work.</summary>
internal sealed partial class OutboxWorkExpander<TWork>(
    OutboxWorkRegistration<TWork> registration,
    ILogger<OutboxWorkExpander<TWork>> logger) : IOutboxSubscriberExpander<OutboxMessageJob<TWork>>
    where TWork : class, IOutboxWork<TWork>
{
    public Task<IReadOnlyList<OutboxSubscriber<OutboxMessageJob<TWork>>>> ExpandAsync(
        IOutboxMessage message,
        long tenantId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<OutboxSubscriber<OutboxMessageJob<TWork>>>>(MayNeedWork(message)
            ? [new OutboxSubscriber<OutboxMessageJob<TWork>>(registration.JobType, new OutboxMessageJob<TWork>(message.Id))]
            : []);
    }

    // Whatever the predicate throws, the job is enqueued and fails on its own, so one work's bad read never blocks
    // the message's other subscribers.
    private bool MayNeedWork(IOutboxMessage message)
    {
        if (registration.OnlyWhen is not { } onlyWhen)
        {
            return true;
        }

        try
        {
            return onlyWhen(message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogReadFailed(logger, typeof(TWork).Name, message.Id, registration.JobType, exception);
            return true;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        EventName = "OutboxWorkReadFailed",
        Message = "Outbox work {Work} could not read message {OutboxMessageId}; its {JobType} job is enqueued to fail on its own")]
    private static partial void LogReadFailed(
        ILogger logger,
        string work,
        long outboxMessageId,
        string jobType,
        Exception exception);
}
