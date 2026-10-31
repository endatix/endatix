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
    public static IServiceCollection AddReportingOutboxWork(this IServiceCollection services) =>
        services
            .AddOutboxWork<CompileFormSchemaOutboxHandler>()
            .AddOutboxWork<FlattenSubmissionOutboxHandler>()
            .AddOutboxWork<SyncSubmissionDeletionOutboxHandler>()
            .AddOutboxWork<SyncFormDeletionOutboxHandler>()
            .AddOutboxWork<RebuildSearchCopyOutboxHandler>()
            // A new tenant's event is app-level; the seeding belongs to the tenant being created.
            .AddOutboxWork<SeedDefaultExportFormatsOutboxHandler>(
                appLevelTenant: SeedDefaultExportFormatsOutboxHandler.Parse);
}
