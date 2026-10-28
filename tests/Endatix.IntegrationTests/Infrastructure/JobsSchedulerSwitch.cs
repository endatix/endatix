using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Endatix.IntegrationTests;

/// <summary>
/// Runs one test host's jobs scheduler, or holds it in standby, for a host whose Jobs module is on. The test host
/// builds the scheduler without starting it, so it fires nothing until a test runs it.
/// </summary>
/// <remarks>
/// Standby alone is not a stop: the scheduler stops acquiring, but a batch it acquired just before still fires. So
/// the switch also vetoes every firing while it is off, and the jobs that ran before it went off are what a stop
/// waits for.
/// </remarks>
internal sealed class JobsSchedulerSwitch(IServiceProvider services)
{
    private static readonly TimeSpan IdlePoll = TimeSpan.FromMilliseconds(100);

    private bool _on;

    // A scheduler that has not run since it last went idle has nothing to wait for.
    private bool _ranSinceIdle;

    /// <summary>Whether a firing may run its job now.</summary>
    public bool IsOn => Volatile.Read(ref _on);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (await SchedulerAsync() is not { } scheduler)
        {
            return;
        }

        if (scheduler.ListenerManager.GetTriggerListener(SwitchedOffVeto.ListenerName) is null)
        {
            scheduler.ListenerManager.AddTriggerListener(new SwitchedOffVeto(this), Matchers.AllTriggers());
        }

        SetOn(true);
        _ranSinceIdle = true;
        await scheduler.Start(cancellationToken);
    }

    /// <summary>
    /// Vetoes every firing from now on and stops acquiring. Not cancellable: a stop that gives up waiting must still
    /// leave the scheduler held.
    /// </summary>
    public async Task StandbyAsync()
    {
        SetOn(false);
        if (await SchedulerAsync() is { Status: SchedulerStatus.Running } scheduler)
        {
            _ranSinceIdle = true;
            await scheduler.Standby(CancellationToken.None);
        }
    }

    /// <summary>
    /// Waits until this node has no firing left that started before the switch went off: none running, and none still
    /// recording its outcome.
    /// </summary>
    public async Task WaitUntilIdleAsync(CancellationToken cancellationToken)
    {
        if (!_ranSinceIdle || await SchedulerAsync() is not { } scheduler)
        {
            return;
        }

        while (!await IsIdleAsync(scheduler, cancellationToken))
        {
            await Task.Delay(IdlePoll, cancellationToken);
        }

        _ranSinceIdle = false;
    }

    private void SetOn(bool on) => Volatile.Write(ref _on, on);

    private async Task<IScheduler?> SchedulerAsync() =>
        services.GetKeyedService<ISchedulerFactory>(QuartzRegistration.SchedulerName) is { } factory
            ? await factory.GetScheduler(CancellationToken.None)
            : null;

    // A firing the switch let through was recorded in the store before its job could run, and its record lasts until
    // its outcome is recorded, after the job and its listeners, so it covers more than the jobs counted as executing.
    // A firing vetoed meanwhile only completes its own record.
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

    /// <summary>
    /// Vetoes a firing while the switch is off. The vetoed trigger completes as a firing that did not run, so a
    /// one-off trigger is gone and its job row waits for the next reset; the scheduler keeps nothing of it.
    /// </summary>
    private sealed class SwitchedOffVeto(JobsSchedulerSwitch jobsSwitch) : ITriggerListener
    {
        public const string ListenerName = "integration-test-switched-off-veto";

        public string Name => ListenerName;

        public ValueTask<bool> VetoJobExecution(
            ITrigger trigger,
            IJobExecutionContext context,
            CancellationToken cancellationToken = default) =>
            new(!jobsSwitch.IsOn);
    }
}
