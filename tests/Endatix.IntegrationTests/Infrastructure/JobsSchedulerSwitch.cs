using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Endatix.IntegrationTests;

/// <summary>
/// Runs one test host's jobs scheduler, or holds it in standby, for a host whose Jobs module is on. The test host
/// builds the scheduler without starting it, so it fires nothing until a test runs it.
/// </summary>
internal sealed class JobsSchedulerSwitch(IServiceProvider services)
{
    private static readonly TimeSpan IdlePoll = TimeSpan.FromMilliseconds(100);

    // A firing the scheduler acquired just before standby only shows once its acquisition commits, so idle has to hold
    // across a pause longer than an acquisition takes.
    private static readonly TimeSpan IdleSettle = TimeSpan.FromMilliseconds(500);

    // Only this switch starts the scheduler, so one it has not run since its last standby has nothing to wait for.
    private bool _ranSinceStandby;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (await SchedulerAsync(cancellationToken) is { } scheduler)
        {
            _ranSinceStandby = true;
            await scheduler.Start(cancellationToken);
        }
    }

    /// <summary>
    /// Puts the scheduler in standby and waits until this node has no firing left: none acquired, none running, and
    /// none still recording its outcome.
    /// </summary>
    public async Task StandbyAsync(CancellationToken cancellationToken)
    {
        if (!_ranSinceStandby || await SchedulerAsync(cancellationToken) is not { } scheduler)
        {
            return;
        }

        await scheduler.Standby(cancellationToken);
        do
        {
            await WaitUntilIdleAsync(scheduler, cancellationToken);
            await Task.Delay(IdleSettle, cancellationToken);
        }
        while (!await IsIdleAsync(scheduler, cancellationToken));

        _ranSinceStandby = false;
    }

    private async Task<IScheduler?> SchedulerAsync(CancellationToken cancellationToken) =>
        services.GetKeyedService<ISchedulerFactory>(QuartzRegistration.SchedulerName) is { } factory
            ? await factory.GetScheduler(cancellationToken)
            : null;

    private static async Task WaitUntilIdleAsync(IScheduler scheduler, CancellationToken cancellationToken)
    {
        while (!await IsIdleAsync(scheduler, cancellationToken))
        {
            await Task.Delay(IdlePoll, cancellationToken);
        }
    }

    // A firing's row in the store lasts from its acquisition until its outcome is recorded, after the job and its
    // listeners have run, so it covers more than the jobs the scheduler counts as executing.
    private static async Task<bool> IsIdleAsync(IScheduler scheduler, CancellationToken cancellationToken)
    {
        var metadata = await scheduler.GetMetadata(cancellationToken);
        if (metadata.LocalExecutingJobs > 0)
        {
            return false;
        }

        var firings = await scheduler.QueryFireInstances(
            new FireInstanceQuery { SchedulerInstanceId = scheduler.SchedulerInstanceId, State = null },
            cancellationToken);
        return firings.Items.Count == 0;
    }
}
