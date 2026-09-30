namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Set once this host has stopped waiting for its running jobs. Every handler's token is linked to it.
/// </summary>
/// <remarks>
/// <para>
/// The scheduler is never asked to cancel a running job at shutdown, because a job that returns while the
/// scheduler is still listening is recorded as finished and never runs again. The signal is raised only after
/// the scheduler has let go of its running jobs, so they stop, write nothing, and run again on the next node.
/// </para>
/// <para>
/// Disposing frees the source without raising the signal: a host torn down without stopping is treated like a
/// crash, whose handlers are never told. A host can still be stopped after its container disposed the signal, so
/// raising it then does nothing rather than throw, and the token stays readable.
/// </para>
/// </remarks>
internal sealed class JobsShutdownSignal : IDisposable
{
    private readonly CancellationTokenSource _source = new();
    private readonly CancellationToken _token;
    private bool _disposed;

    public JobsShutdownSignal() => _token = _source.Token;

    public CancellationToken Token => _token;

    public bool IsRaised => _token.IsCancellationRequested;

    public void Raise()
    {
        if (_disposed || _token.IsCancellationRequested)
        {
            return;
        }

        try
        {
            _source.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Disposed between the check and the cancel; there is nothing left to tell.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _source.Dispose();
    }
}
