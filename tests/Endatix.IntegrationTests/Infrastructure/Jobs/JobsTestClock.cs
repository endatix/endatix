using Endatix.Core.Abstractions;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>
/// One fake clock for every node of a test: the jobs scheduler's clock and the host's
/// <see cref="IDateTimeProvider"/>. It starts at real now and stands still until the test steps it.
/// </summary>
/// <remarks>
/// <para>
/// For jobs-only hosts (<see cref="JobsTestNode"/>) only. In a web host, <see cref="IDateTimeProvider"/> also times
/// tokens and audit records, which a fake clock would move too.
/// </para>
/// <para>
/// One step moves every scheduler timer at once. Steps stay below the misfire threshold, or a due trigger is
/// counted as a misfire, and below the check-in threshold, or a live peer looks dead. Shutdown waits stay real time.
/// </para>
/// </remarks>
internal sealed class JobsTestClock
{
    /// <summary>
    /// Scheduler timings long beside <see cref="Step"/>, so a step wakes the idle wait and the store's check-in no
    /// more than it must, and a live node never looks dead after one step. The default misfire threshold of 60 s stays.
    /// </summary>
    public static Dictionary<string, string?> SlowSchedulerTimings() => new()
    {
        ["Endatix:BackgroundJobs:IdleWaitTimeSeconds"] = "30",
        ["Endatix:BackgroundJobs:Clustering:CheckinIntervalSeconds"] = "30",
        ["Endatix:BackgroundJobs:Clustering:CheckinMisfireThresholdSeconds"] = "30",
    };

    /// <summary>A step below the default misfire threshold and the long check-in threshold.</summary>
    public static readonly TimeSpan Step = TimeSpan.FromSeconds(20);

    // Real time between steps, for the scheduler thread and the job's pool thread to act on the last one.
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(100);

    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);

    public TimeProvider Time => _time;

    /// <summary>Puts this clock behind the scheduler's key and <see cref="IDateTimeProvider"/>.</summary>
    public void Use(IServiceCollection services)
    {
        services.RemoveAllKeyed<TimeProvider>(QuartzRegistration.SchedulerName);
        services.AddKeyedSingleton<TimeProvider>(QuartzRegistration.SchedulerName, _time);
        services.Replace(ServiceDescriptor.Singleton<IDateTimeProvider>(new ClockDateTimeProvider(_time)));
    }

    /// <summary>
    /// Steps the clock by <see cref="Step"/> until <paramref name="done"/> holds, or until it has moved
    /// <paramref name="limit"/>.
    /// </summary>
    public async Task<bool> StepUntilAsync(Func<bool> done, TimeSpan limit, CancellationToken cancellationToken)
    {
        for (var moved = TimeSpan.Zero; moved < limit && !done(); moved += Step)
        {
            _time.Advance(Step);
            await Task.Delay(Settle, cancellationToken);
        }

        return done();
    }

    /// <summary>
    /// Moves the clock by <paramref name="by"/> at once, which may be past the misfire threshold on purpose, then
    /// gives the scheduler's threads real time to act on it.
    /// </summary>
    public async Task AdvanceAsync(TimeSpan by, CancellationToken cancellationToken)
    {
        _time.Advance(by);
        await Task.Delay(Settle, cancellationToken);
    }

    private sealed class ClockDateTimeProvider(TimeProvider clock) : IDateTimeProvider
    {
        public DateTimeOffset Now => clock.GetUtcNow();
    }
}
