using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Infrastructure.Features.BackgroundJobs;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Audience.Features.Forms;
using Endatix.Modules.Audience.Features.TestSubmissions;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Audience.Features.BackgroundJobs;

/// <summary>
/// Registers Audience's outbox work: the inline handlers the relay runs, and the same work as background jobs with
/// the subscriptions that enqueue them. Which one delivers is the relay's switch.
/// </summary>
internal static class AudienceOutboxJobs
{
    // Each Audience job is one short write, so ten minutes is ample for an attempt, and five attempts doubling from
    // 10 seconds retry for about two and a half minutes.
    internal static readonly BackgroundJobTypeDefaults JobDefaults = new()
    {
        MaxAttempts = 5,
        MaxRuntimeMinutes = 10,
        BackoffBaseSeconds = 10,
        BackoffCapSeconds = 600,
        RetentionDays = 3,
        MaxConcurrency = 2,
    };

    public static IServiceCollection AddAudienceOutboxWork(this IServiceCollection services)
    {
        AddInlineHandlers(services);
        AddJobHandlers(services);
        AddSubscriptions(services);
        return services;
    }

    // Each inline handler is registered as itself too, so its job handler reuses the very same work.
    private static void AddInlineHandlers(IServiceCollection services)
    {
        AddInline<DeleteFormAudienceOutboxHandler>(services);
        AddInline<ReleaseLinkOnTestMarkOutboxHandler>(services);
    }

    private static void AddJobHandlers(IServiceCollection services)
    {
        services.AddBackgroundJobHandler<DeleteFormAudienceJobHandler, AudienceDeleteFormAudiencePayload>(JobDefaults);
        services.AddBackgroundJobHandler<ReleaseLinkOnTestMarkJobHandler, AudienceReleaseLinkOnTestMarkPayload>(JobDefaults);
    }

    private static void AddSubscriptions(IServiceCollection services)
    {
        Subscribe(services, DeleteFormAudienceOutboxHandler.HandledEventTypes,
            message => new AudienceDeleteFormAudiencePayload(message.Id));

        // Most submission updates leave the test flag alone; the expander enqueues a job only for one that changes it.
        foreach (var eventType in ReleaseLinkOnTestMarkOutboxHandler.HandledEventTypes)
        {
            services.AddOutboxJobSubscription<AudienceReleaseLinkOnTestMarkPayload, ReleaseLinkOnTestMarkExpander>(eventType);
        }
    }

    private static void AddInline<THandler>(IServiceCollection services)
        where THandler : class, IOutboxIntegrationEventHandler
    {
        services.AddScoped<THandler>();
        services.AddScoped<IOutboxIntegrationEventHandler>(provider => provider.GetRequiredService<THandler>());
    }

    private static void Subscribe<TPayload>(
        IServiceCollection services,
        IEnumerable<string> eventTypes,
        Func<IOutboxMessage, TPayload> createPayload)
        where TPayload : IBackgroundJobPayload
    {
        foreach (var eventType in eventTypes)
        {
            services.AddOutboxJobSubscription(eventType, createPayload);
        }
    }
}
