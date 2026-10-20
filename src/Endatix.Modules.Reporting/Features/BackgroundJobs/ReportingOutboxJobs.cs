using Endatix.Infrastructure.Features.BackgroundJobs;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Reporting.Features.Outbox;
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

    public static IServiceCollection AddReportingOutboxWork(this IServiceCollection services) =>
        services
            .AddOutboxWork<CompileFormSchemaOutboxHandler, ReportingCompileFormSchemaPayload>(JobDefaults)
            .AddOutboxWork<FlattenSubmissionOutboxHandler, ReportingFlattenSubmissionPayload>(JobDefaults)
            .AddOutboxWork<SyncSubmissionDeletionOutboxHandler, ReportingSyncSubmissionDeletionPayload>(JobDefaults)
            .AddOutboxWork<SyncFormDeletionOutboxHandler, ReportingSyncFormDeletionPayload>(JobDefaults)
            // A new tenant's event is app-level; the seeding belongs to the tenant being created.
            .AddOutboxWork<SeedDefaultExportFormatsOutboxHandler, ReportingSeedDefaultExportFormatsPayload>(
                JobDefaults, options => options.AppLevel(SeedDefaultExportFormatsOutboxHandler.Parse));
}
