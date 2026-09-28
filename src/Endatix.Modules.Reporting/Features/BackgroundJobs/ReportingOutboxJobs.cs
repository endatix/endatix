using System.Text.Json;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Events;
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
    public static IServiceCollection AddReportingOutboxWork(this IServiceCollection services)
    {
        // Each inline handler is registered as itself too, so its job handler reuses the very same work.
        AddInline<CompileFormSchemaOutboxHandler>(services);
        AddInline<FlattenSubmissionOutboxHandler>(services);
        AddInline<SyncSubmissionDeletionOutboxHandler>(services);
        AddInline<SyncFormDeletionOutboxHandler>(services);
        AddInline<SeedDefaultExportFormatsOutboxHandler>(services);

        services.AddScoped<IBackgroundJobHandler, CompileFormSchemaJobHandler>();
        services.AddScoped<IBackgroundJobHandler, FlattenSubmissionJobHandler>();
        services.AddScoped<IBackgroundJobHandler, SeedDefaultExportFormatsJobHandler>();
        services.AddScoped<IBackgroundJobHandler, SyncFormDeletionJobHandler>();
        services.AddScoped<IBackgroundJobHandler, SyncSubmissionDeletionJobHandler>();

        Subscribe(services, CompileFormSchemaOutboxHandler.HandledEventTypes,
            message => new ReportingCompileFormSchemaPayload(message.Id));
        Subscribe(services, FlattenSubmissionOutboxHandler.HandledEventTypes,
            message => new ReportingFlattenSubmissionPayload(message.Id));
        Subscribe(services, SyncFormDeletionOutboxHandler.HandledEventTypes,
            message => new ReportingSyncFormDeletionPayload(message.Id));
        Subscribe(services, SyncSubmissionDeletionOutboxHandler.HandledEventTypes,
            message => new ReportingSyncSubmissionDeletionPayload(message.Id));

        // A new tenant's event is app-level; the seeding belongs to the tenant being created.
        Subscribe(services, SeedDefaultExportFormatsOutboxHandler.HandledEventTypes,
            message => new ReportingSeedDefaultExportFormatsPayload(message.Id),
            TenantBeingCreated);

        return services;
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
        Func<IOutboxMessage, TPayload> createPayload,
        Func<IOutboxMessage, long>? resolveTenantId = null)
        where TPayload : IBackgroundJobPayload
    {
        foreach (var eventType in eventTypes)
        {
            services.AddOutboxJobSubscription(eventType, createPayload, resolveTenantId);
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
