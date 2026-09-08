namespace Endatix.Core.Abstractions.BackgroundJobs;

/// <summary>
/// Lifecycle state of a background job. The job row is the single source of truth for this value —
/// nothing about a job's state lives in a second system.
/// </summary>
/// <remarks>
/// Transitions: <see cref="Pending"/> → <see cref="Processing"/> → <see cref="Completed"/> |
/// <see cref="Failed"/> | <see cref="DeadLettered"/> | <see cref="Retrying"/> |
/// <see cref="Canceled"/>, and <see cref="Retrying"/> → <see cref="Processing"/>. <see cref="Canceled"/> is also reachable
/// directly from <see cref="Pending"/> or <see cref="Retrying"/> — a job cancelled before it is
/// claimed never runs at all.
/// <para>
/// <see cref="Pending"/> and <see cref="Retrying"/> both mean <em>eligible to run at
/// NextAttemptAt</em>, so one query dispatches either.
/// </para>
/// </remarks>
public enum JobStatus
{
    /// <summary>Enqueued, never started.</summary>
    Pending = 0,

    /// <summary>Claimed by a runner; a heartbeat is expected while it runs.</summary>
    Processing = 1,

    /// <summary>An attempt failed retryably; waits until NextAttemptAt.</summary>
    Retrying = 2,

    /// <summary>Terminal. Success; the result payload is populated.</summary>
    Completed = 3,

    /// <summary>
    /// Terminal. A <b>deterministic</b> failure — retrying cannot help. Distinct from
    /// <see cref="DeadLettered"/> because a single status cannot express "do not retry this"
    /// versus "retried and gave up", and both sweeper queries and ops dashboards need to tell
    /// them apart.
    /// </summary>
    Failed = 4,

    /// <summary>Terminal. A retryable failure that exhausted its attempt budget.</summary>
    DeadLettered = 5,

    /// <summary>Terminal. Canceled by a user or by host shutdown.</summary>
    Canceled = 6,
}
