using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Infrastructure.Features.BackgroundJobs;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// Registers a module's outbox work: the inline handler the relay runs, the same work as a background job, and the
/// subscriptions that enqueue it. Which one delivers is the relay's switch (<c>Endatix:Outbox:DeliverToJobQueue</c>).
/// </summary>
public static class OutboxWorkServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TWork"/> as an inline handler, its job keyed by
    /// <typeparamref name="TPayload"/>'s job type with <paramref name="defaults"/>, and a job subscription for every
    /// event type in <see cref="IOutboxWork.HandledEventTypes"/>.
    /// </summary>
    public static IServiceCollection AddOutboxWork<TWork, TPayload>(
        this IServiceCollection services,
        BackgroundJobTypeDefaults defaults,
        Action<OutboxWorkOptions>? configure = null)
        where TWork : class, IOutboxWork
        where TPayload : IOutboxJobPayload<TPayload>
    {
        ArgumentNullException.ThrowIfNull(defaults);

        var options = new OutboxWorkOptions();
        configure?.Invoke(options);
        services.AddInlineOutboxHandler<TWork>();
        AddJob<TWork, TPayload>(services, defaults, options);
        foreach (var eventType in TWork.HandledEventTypes)
        {
            Subscribe<TWork, TPayload>(services, eventType, options);
        }

        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="THandler"/> inline only, as itself and as an
    /// <see cref="IOutboxIntegrationEventHandler"/>, for a module whose work has no job yet. With
    /// <c>Endatix:Outbox:DeliverToJobQueue</c> on, the host refuses to start while its events have no subscription;
    /// an <see cref="IOutboxWork"/> registered this way never has one, so register it with <c>AddOutboxWork</c>.
    /// </summary>
    public static IServiceCollection AddInlineOutboxHandler<THandler>(this IServiceCollection services)
        where THandler : class, IOutboxIntegrationEventHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(THandler)))
        {
            throw new InvalidOperationException(
                $"The outbox handler {typeof(THandler).FullName} is already registered; register it once, so it runs once.");
        }

        // Registered as itself too, so a job in the same scope reuses the very same work.
        services.AddScoped<THandler>();
        services.AddScoped<IOutboxIntegrationEventHandler>(provider => provider.GetRequiredService<THandler>());
        return services;
    }

    private static void AddJob<TWork, TPayload>(
        IServiceCollection services,
        BackgroundJobTypeDefaults defaults,
        OutboxWorkOptions options)
        where TWork : class, IOutboxWork
        where TPayload : IOutboxJobPayload<TPayload>
    {
        services.AddBackgroundJobHandler<OutboxJobHandler<TPayload, TWork>, TPayload>(defaults);
        services.TryAddScoped<OutboxJobMessages>();
        services.Configure<OutboxWorkSettings>(TPayload.JobType, settings => settings.ReadTenant = options.ReadsTenant);
    }

    private static void Subscribe<TWork, TPayload>(IServiceCollection services, string eventType, OutboxWorkOptions options)
        where TWork : class, IOutboxWork
        where TPayload : IOutboxJobPayload<TPayload>
    {
        var source = new OutboxSubscriptionServiceCollectionExtensions.SubscriptionSource(
            eventType,
            options.ReadsTenant is { } readTenant ? TenantOrNone(readTenant) : null,
            typeof(TWork));
        if (options.SkipsMessagesWithoutWork)
        {
            services.AddExpandingJobSubscription<TPayload, OutboxWorkExpander<TPayload, TWork>>(source);
        }
        else
        {
            services.AddJobSubscription(source, message => TPayload.For(message.Id));
        }
    }

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

/// <summary>How <see cref="OutboxWorkServiceCollectionExtensions.AddOutboxWork{TWork, TPayload}"/> registers one work.</summary>
public sealed class OutboxWorkOptions
{
    internal Func<IOutboxMessage, long>? ReadsTenant { get; private set; }

    internal bool SkipsMessagesWithoutWork { get; private set; }

    /// <summary>
    /// The work's messages are app-level (their row carries no tenant), and each job belongs to the tenant
    /// <paramref name="readTenantId"/> reads from the payload.
    /// </summary>
    public OutboxWorkOptions AppLevel(Func<IOutboxMessage, long> readTenantId)
    {
        ArgumentNullException.ThrowIfNull(readTenantId);
        ReadsTenant = readTenantId;
        return this;
    }

    /// <summary>
    /// Enqueues no job for a message the work reads as <see cref="OutboxWork.None"/>, so a frequent event that rarely
    /// needs the work adds no empty jobs. A message the work cannot read, or whose read throws anything else, is
    /// enqueued anyway, so its job fails visibly and the message's other subscribers are unaffected.
    /// The work is resolved and read in the relay's scope.
    /// </summary>
    public OutboxWorkOptions OnlyWhenThereIsWork()
    {
        SkipsMessagesWithoutWork = true;
        return this;
    }
}

/// <summary>What an outbox work's job needs to know of its registration, named by the job type.</summary>
internal sealed class OutboxWorkSettings
{
    /// <summary>
    /// Reads the job's tenant from an app-level row's payload; <see langword="null"/> when the rows carry their tenant.
    /// See <see cref="OutboxWorkOptions.AppLevel"/>.
    /// </summary>
    public Func<IOutboxMessage, long>? ReadTenant { get; set; }
}

/// <summary>Subscribes the job of <typeparamref name="TWork"/> only for a message that may need its work.</summary>
internal sealed class OutboxWorkExpander<TPayload, TWork>(TWork work) : IOutboxSubscriberExpander<TPayload>
    where TPayload : IOutboxJobPayload<TPayload>
    where TWork : class, IOutboxWork
{
    public Task<IReadOnlyList<OutboxSubscriber<TPayload>>> ExpandAsync(
        IOutboxMessage message,
        long tenantId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<OutboxSubscriber<TPayload>>>(MayNeedWork(message)
            ? [new OutboxSubscriber<TPayload>(TPayload.JobType, TPayload.For(message.Id))]
            : []);
    }

    // Whatever Read throws, the job is enqueued and fails on its own, so one work's bad read never blocks the
    // message's other subscribers.
    private bool MayNeedWork(IOutboxMessage message)
    {
        try
        {
            return !work.Read(message).IsNone;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return true;
        }
    }
}
