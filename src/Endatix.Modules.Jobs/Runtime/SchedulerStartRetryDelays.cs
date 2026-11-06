namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// How long a scheduler whose start failed waits before it tries again, by kind of failure.
/// </summary>
/// <param name="Unreachable">After an outage, and after a lost race that keeps coming back.</param>
/// <param name="Contended">
/// After one of the first few lost races: a lost race clears as soon as the other transaction ends, so it is tried
/// again sooner than an outage.
/// </param>
internal sealed record SchedulerStartRetryDelays(TimeSpan Unreachable, TimeSpan Contended)
{
    /// <summary>The delays every host runs with.</summary>
    public static SchedulerStartRetryDelays Default { get; } = new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1));
}
