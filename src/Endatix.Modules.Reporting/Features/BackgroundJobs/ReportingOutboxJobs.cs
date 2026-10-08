using System.Text.Json;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Events;
using Endatix.Infrastructure.Features.BackgroundJobs;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Reporting.Features.Outbox;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Reporting.Features.BackgroundJobs;

/// <summary>
/// Registers Reporting's outbox work: the inline handlers the relay runs, and the same work as background jobs
/// with the subscriptions that enqueue them. Which one delivers is the relay's switch.
/// </summary>
internal static class ReportingOutboxJobs
{
    // Every Reporting job is one short read-model update, so ten minutes is ample for an attempt, and five attempts
    // doubling from 10 seconds retry for about two and a half minutes.
    private static readonly BackgroundJobTypeDefaults JobDefaults = new()
    {
        MaxAttempts = 5,
        MaxRuntimeMinutes = 10,
        BackoffBaseSeconds = 10,
        BackoffCapSeconds = 600,
        RetentionDays = 3,
        MaxConcurrency = 2,
    };

    public static IServiceCollection AddReportingOutboxWork(this IServiceCollection services)
    {
        AddInlineHandlers(services);
        AddJobHandlers(services);
        AddSubscriptions(services);
        return services;
    }

    // Each inline handler is registered as itself too, so its job handler reuses the very same work.
    private static void AddInlineHandlers(IServiceCollection services)
    {
        AddInline<CompileFormSchemaOutboxHandler>(services);
        AddInline<FlattenSubmissionOutboxHandler>(services);
        AddInline<SyncSubmissionDeletionOutboxHandler>(services);
        AddInline<SyncFormDeletionOutboxHandler>(services);
        AddInline<SeedDefaultExportFormatsOutboxHandler>(services);
    }

    private static void AddJobHandlers(IServiceCollection services)
    {
        services.AddBackgroundJobHandler<CompileFormSchemaJobHandler, ReportingCompileFormSchemaPayload>(JobDefaults);
        services.AddBackgroundJobHandler<FlattenSubmissionJobHandler, ReportingFlattenSubmissionPayload>(JobDefaults);
        services.AddBackgroundJobHandler<SeedDefaultExportFormatsJobHandler, ReportingSeedDefaultExportFormatsPayload>(JobDefaults);
        services.AddBackgroundJobHandler<SyncFormDeletionJobHandler, ReportingSyncFormDeletionPayload>(JobDefaults);
        services.AddBackgroundJobHandler<SyncSubmissionDeletionJobHandler, ReportingSyncSubmissionDeletionPayload>(JobDefaults);
    }

    private static void AddSubscriptions(IServiceCollection services)
    {
        Subscribe(services, CompileFormSchemaOutboxHandler.HandledEventTypes,
            message => new ReportingCompileFormSchemaPayload(message.Id));
        Subscribe(services, FlattenSubmissionOutboxHandler.HandledEventTypes,
            message => new ReportingFlattenSubmissionPayload(message.Id));
        Subscribe(services, SyncFormDeletionOutboxHandler.HandledEventTypes,
            message => new ReportingSyncFormDeletionPayload(message.Id));
        Subscribe(services, SyncSubmissionDeletionOutboxHandler.HandledEventTypes,
            message => new ReportingSyncSubmissionDeletionPayload(message.Id));

        // A new tenant's event is app-level; the seeding belongs to the tenant being created.
        foreach (var eventType in SeedDefaultExportFormatsOutboxHandler.HandledEventTypes)
        {
            services.AddOutboxJobSubscription(
                eventType, message => new ReportingSeedDefaultExportFormatsPayload(message.Id), TenantBeingCreated);
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

    // An unreadable tenant resolves to none, which fails the publish, so the relay retries and then fails the
    // message rather than writing a job without a tenant.
    private static long TenantBeingCreated(IOutboxMessage message)
    {
        try
        {
            return SeedDefaultExportFormatsOutboxHandler.Parse(message);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException)
        {
            return 0;
        }
    }
}
