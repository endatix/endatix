namespace Endatix.Modules.Jobs.Runtime;

internal static class BackgroundJobRetryPolicy
{
    /// <summary>
    /// Returns <paramref name="utcNow"/> plus the policy's backoff base, doubled once for every attempt before
    /// <paramref name="claimedAttempt"/> and never longer than the policy's backoff cap. The attempt count goes up
    /// when a job is claimed rather than when it fails, so the first failure is already attempt 1 and waits the
    /// base delay, not twice it.
    /// </summary>
    public static DateTime NextAttemptAt(int claimedAttempt, DateTime utcNow, BackgroundJobTypePolicy policy)
    {
        // Compared before subtracting, so an attempt of int.MinValue cannot wrap round to a huge exponent.
        var exponent = claimedAttempt <= 1 ? 0 : claimedAttempt - 1;

        // In double arithmetic the doubling overflows to infinity instead of throwing, leaving the cap as the
        // smaller of the two.
        var delaySeconds = Math.Min(
            policy.BackoffCap.TotalSeconds,
            policy.BackoffBase.TotalSeconds * Math.Pow(2, exponent));

        return utcNow.AddSeconds(delaySeconds);
    }
}
