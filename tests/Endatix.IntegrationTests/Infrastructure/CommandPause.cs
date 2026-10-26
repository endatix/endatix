using System.Text.RegularExpressions;

namespace Endatix.IntegrationTests.Infrastructure;

/// <summary>
/// One armed pause. Disposing it releases the paused command and disarms the gate, so a failed assert never leaves
/// a request held into the next test.
/// </summary>
public sealed class CommandPause(Regex statement, AppCommandGate gate, Exception? fault = null) : IDisposable
{
    private int _matches;

    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when a command reached the pause.</summary>
    public Task Reached => _reached.Task;

    /// <summary>Lets the paused command run.</summary>
    public void Release() => _released.TrySetResult();

    /// <inheritdoc />
    public void Dispose()
    {
        gate.Disarm(this);
        Release();
    }

    /// <summary>What the paused command fails with once released, if anything.</summary>
    internal Exception? Fault => fault;

    internal bool Matches(string commandText) => statement.IsMatch(commandText);

    /// <summary>How many matching commands run before the one this pause catches.</summary>
    internal int Skip { get; init; }

    /// <summary>Catches this match, once the <see cref="Skip"/> matches before it have run.</summary>
    internal bool TryCatch() => Interlocked.Increment(ref _matches) > Skip && _reached.TrySetResult();

    internal Task WaitForReleaseAsync(CancellationToken cancellationToken) => _released.Task.WaitAsync(cancellationToken);
}
