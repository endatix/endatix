namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// One attempt of one job: what a fenced write matches the row on, so a caller that lost the job changes nothing.
/// </summary>
internal readonly record struct AttemptRef(long JobId, int AttemptCount);

/// <summary>A claim of a job row for a new attempt.</summary>
/// <param name="JobId">The row to claim.</param>
/// <param name="RegisteredJobTypes">The job types this host has handlers for; a row of any other type is left.</param>
/// <param name="UtcNow">When the claim is made.</param>
/// <param name="Recovering">
/// Whether a row still <c>Processing</c> under the attempt it was read at is claimed again, as a recovery does.
/// </param>
internal sealed record JobClaim(
    long JobId,
    IReadOnlyCollection<string> RegisteredJobTypes,
    DateTime UtcNow,
    bool Recovering = false);

/// <summary>Why an attempt failed, as the row keeps it, and when the failure is recorded.</summary>
internal readonly record struct AttemptFailure(string ErrorMessage, DateTime UtcNow);

/// <summary>
/// A failure judged against the job's attempt budget: retried while attempts remain, else dead-lettered.
/// </summary>
/// <param name="Failure">The failure the row records either way.</param>
/// <param name="MaxAttempts">The job type's attempt budget.</param>
/// <param name="NextAttemptAt">When the next attempt is due, should one remain.</param>
internal readonly record struct RetryableFailure(AttemptFailure Failure, int MaxAttempts, DateTime NextAttemptAt);
