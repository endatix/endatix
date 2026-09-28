namespace Endatix.Modules.Jobs.Runtime;

/// <summary>How one attempt of a job ended, as the wrapper observed it.</summary>
internal enum AttemptEnd
{
    /// <summary>The handler returned success.</summary>
    Succeeded,

    /// <summary>The handler returned a failure: deterministic, never retried.</summary>
    ReturnedFailure,

    /// <summary>The handler threw, or ran past its runtime ceiling: presumed transient.</summary>
    Threw,

    /// <summary>The row was set to <c>Canceled</c> while the handler ran.</summary>
    Canceled,

    /// <summary>The host stopped waiting for the job before it ended.</summary>
    HostShutdown,
}

/// <summary>What the wrapper writes to the row for an attempt.</summary>
internal enum AttemptRowWrite
{
    /// <summary>Nothing: the row keeps the status it has.</summary>
    None,
    Completed,
    Failed,
    Retrying,
    DeadLettered,
}

/// <param name="Row">The write that records the attempt.</param>
/// <param name="Rethrow">
/// Whether the wrapper throws to the scheduler so the trigger's retry policy schedules the next attempt. Every
/// other outcome returns normally, so the scheduler schedules nothing more.
/// </param>
internal readonly record struct AttemptDecision(AttemptRowWrite Row, bool Rethrow)
{
    /// <summary>
    /// Decides how an attempt is recorded. The row's attempt count, not the scheduler's retry counter, decides
    /// dead-lettering, because a run recovered after a crash consumes an attempt the scheduler never counts.
    /// </summary>
    public static AttemptDecision Decide(AttemptEnd end, int attemptCount, int maxAttempts) => end switch
    {
        AttemptEnd.Succeeded => new(AttemptRowWrite.Completed, Rethrow: false),
        AttemptEnd.ReturnedFailure => new(AttemptRowWrite.Failed, Rethrow: false),
        AttemptEnd.Threw when attemptCount < maxAttempts => new(AttemptRowWrite.Retrying, Rethrow: true),
        AttemptEnd.Threw => new(AttemptRowWrite.DeadLettered, Rethrow: false),
        AttemptEnd.Canceled => new(AttemptRowWrite.None, Rethrow: false),
        AttemptEnd.HostShutdown => new(AttemptRowWrite.None, Rethrow: false),
        _ => throw new ArgumentOutOfRangeException(nameof(end), end, null),
    };
}
