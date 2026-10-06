using Quartz;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>
/// A scheduler job whose every firing throws to the scheduler, as the job wrapper of earlier versions did to have a
/// trigger's retry policy run the next attempt.
/// </summary>
internal sealed class ThrowingJob : IJob
{
    public ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromException(new JobExecutionException("The attempt failed and is left to the trigger's retry policy."));
}
