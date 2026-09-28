using System.Globalization;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// The only scheduler job class. One durable scheduler job per job type points at it, and each trigger carries
/// only the id of the job row it fires.
/// </summary>
/// <remarks>
/// Handlers never see this class or any scheduler type: everything scheduler-specific stays here, so replacing
/// the scheduler rewrites this class and not the handlers.
/// </remarks>
internal sealed class BackgroundJobExecution(
    IServiceScopeFactory scopeFactory,
    JobHandlerRegistry registry,
    IDateTimeProvider dateTimeProvider) : IJob
{
    /// <summary>The trigger data key that holds the job row's id.</summary>
    public const string JobIdKey = "jobId";

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var jobId = long.Parse(context.MergedJobDataMap.GetString(JobIdKey)!, CultureInfo.InvariantCulture);

        ClaimedJob? claimed;
        await using (var claimScope = scopeFactory.CreateAsyncScope())
        {
            var repository = claimScope.ServiceProvider.GetRequiredService<IBackgroundJobStateRepository>();
            claimed = await repository.TryClaimAsync(
                jobId, registry.JobTypes, dateTimeProvider.UtcNow.UtcDateTime, cancellationToken);
        }

        // Nothing to run: the job already ran, is running elsewhere, or was cancelled.
        if (claimed is null)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var handler = registry.Resolve(scope.ServiceProvider, claimed.JobType)!;
        await handler.ExecuteAsync(
            new BackgroundJobContext(claimed.Id, claimed.JobType, claimed.TenantId, claimed.PayloadJson, claimed.AttemptCount),
            cancellationToken);
    }
}
