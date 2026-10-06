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

    /// <summary>The row was taken over by another attempt, or is gone, while the handler ran.</summary>
    Superseded,
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

/// <summary>Decides what the wrapper writes to the row for an attempt.</summary>
internal static class AttemptDecision
{
    /// <summary>
    /// Decides how an attempt is recorded. The row's attempt count, against the job type's attempt budget as this
    /// node is configured now, decides dead-lettering, so a budget raised while a job waits gives it the attempts
    /// it added.
    /// </summary>
    public static AttemptRowWrite Decide(AttemptEnd end, int attemptCount, int maxAttempts) => end switch
    {
        AttemptEnd.Succeeded => AttemptRowWrite.Completed,
        AttemptEnd.ReturnedFailure => AttemptRowWrite.Failed,
        AttemptEnd.Threw when attemptCount < maxAttempts => AttemptRowWrite.Retrying,
        AttemptEnd.Threw => AttemptRowWrite.DeadLettered,
        AttemptEnd.Canceled => AttemptRowWrite.None,
        AttemptEnd.HostShutdown => AttemptRowWrite.None,
        AttemptEnd.Superseded => AttemptRowWrite.None,
        _ => throw new ArgumentOutOfRangeException(nameof(end), end, null),
    };
}
