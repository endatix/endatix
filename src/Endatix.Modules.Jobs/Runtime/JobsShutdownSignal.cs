namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Set once this host has stopped waiting for its running jobs. Every handler's token is linked to it.
/// </summary>
/// <remarks>
/// The scheduler is never asked to cancel a running job at shutdown, because a job that returns while the
/// scheduler is still listening is recorded as finished and never runs again. The signal is raised only after
/// the scheduler has let go of its running jobs, so they stop, write nothing, and run again on the next node.
/// </remarks>
// Deliberately not disposable: a host can be stopped after its container is disposed, and raising the signal
// then must still work. The source owns no timer, so there is nothing to release.
internal sealed class JobsShutdownSignal
{
    private readonly CancellationTokenSource _source = new();

    public CancellationToken Token => _source.Token;

    public bool IsRaised => _source.IsCancellationRequested;

    public void Raise() => _source.Cancel();
}
