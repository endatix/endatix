using Endatix.Outbox.Engine;

namespace Endatix.IntegrationTests;

/// <summary>
/// One test host's switch for its outbox relay, off until a test turns it on. Per host rather than the
/// <c>Endatix:Outbox:RunInProcess</c> flag, which feeds the process-wide OpenFeature API and so every host at once.
/// </summary>
/// <remarks>
/// Each relay tick that finds the switch on holds a lease until the tick's scope ends, which is after its handlers
/// and its MarkSent. Turning the switch off therefore waits for the batch already running instead of cutting it off
/// halfway through a handler's transaction.
/// </remarks>
internal sealed class OutboxRelaySwitch
{
    private readonly Lock _sync = new();
    private bool _on;
    private int _leases;
    private int _checks;
    private TaskCompletionSource _released = Released();

    /// <summary>How many times this host's relay has asked whether to run.</summary>
    public int Checks => Volatile.Read(ref _checks);

    public bool IsOn
    {
        get
        {
            lock (_sync)
            {
                return _on;
            }
        }
    }

    /// <summary>Whether no relay tick or delivery in the test holds a lease, so none is part-way through a batch.</summary>
    public bool IsIdle
    {
        get
        {
            lock (_sync)
            {
                return _leases == 0;
            }
        }
    }

    public void TurnOn()
    {
        lock (_sync)
        {
            _on = true;
        }
    }

    /// <summary>A lease for one relay tick, or <see langword="null"/> while the switch is off.</summary>
    public IDisposable? TryEnter()
    {
        Interlocked.Increment(ref _checks);
        lock (_sync)
        {
            return _on ? NewLease() : null;
        }
    }

    /// <summary>
    /// A lease for a delivery the test runs itself, or <see langword="null"/> while the relay is on or a tick is
    /// part-way through a batch. It counts with the relay's own leases, so a stop waits for the delivery too.
    /// </summary>
    public IDisposable? TryEnterDelivery()
    {
        lock (_sync)
        {
            return _on || _leases > 0 ? null : NewLease();
        }
    }

    /// <summary>Turns the switch off; the task completes once every tick that found it on has ended.</summary>
    public Task TurnOffAsync()
    {
        lock (_sync)
        {
            _on = false;
            return _released.Task;
        }
    }

    private static TaskCompletionSource Released()
    {
        TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        released.SetResult();
        return released;
    }

    // Called under the lock.
    private Lease NewLease()
    {
        if (_leases++ == 0)
        {
            _released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        return new Lease(this);
    }

    private void Leave()
    {
        lock (_sync)
        {
            if (--_leases == 0)
            {
                _released.SetResult();
            }
        }
    }

    private sealed class Lease(OutboxRelaySwitch relaySwitch) : IDisposable
    {
        private int _left;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _left, 1) == 0)
            {
                relaySwitch.Leave();
            }
        }
    }
}

/// <summary>
/// The host's own relay gate behind its <see cref="OutboxRelaySwitch"/>: asked only while the switch is on. Scoped as
/// the engine resolves its gate, once per tick, so the scope that ends the tick also ends its lease.
/// </summary>
internal sealed class SwitchedOutboxRelayGate(IOutboxRelayGate own, OutboxRelaySwitch relaySwitch)
    : IOutboxRelayGate, IDisposable
{
    private IDisposable? _lease;

    public Task<bool> IsRelayEnabledAsync(CancellationToken cancellationToken)
    {
        _lease ??= relaySwitch.TryEnter();
        return _lease is null ? Task.FromResult(false) : own.IsRelayEnabledAsync(cancellationToken);
    }

    public void Dispose() => _lease?.Dispose();
}
