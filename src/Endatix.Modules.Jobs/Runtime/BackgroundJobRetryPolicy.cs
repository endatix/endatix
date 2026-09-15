namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Decides when a job whose attempt failed retryably becomes eligible again.
/// </summary>
internal static class BackgroundJobRetryPolicy
{
    // Bounds the doubling so the arithmetic stays finite for any attempt count. At this exponent a one-second
    // base already exceeds int.MaxValue seconds, the largest cap the options accept.
    private const int MaxExponent = 31;

    /// <summary>
    /// Returns <paramref name="utcNow"/> plus the retry delay for <paramref name="claimedAttempt"/>: the policy's
    /// backoff base, doubled once for every attempt before this one, and never longer than its backoff cap.
    /// </summary>
    /// <param name="claimedAttempt">The attempt count the job row held once this attempt was claimed.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <param name="policy">The resolved policy for the job's type.</param>
    /// <remarks>
    /// The exponent is the claimed attempt minus one. The attempt count goes up when a job is claimed, not when
    /// it fails, so a first attempt that fails is already attempt 1 and has to wait exactly the base delay. An
    /// attempt below 1 is treated as the first attempt, so it waits the base delay rather than a shorter one and
    /// does not throw.
    /// </remarks>
    public static DateTime NextAttemptAt(int claimedAttempt, DateTime utcNow, BackgroundJobTypePolicy policy)
    {
        // Compared before subtracting, so an attempt of int.MinValue cannot wrap round to the largest exponent.
        var exponent = claimedAttempt <= 1 ? 0 : Math.Min(claimedAttempt - 1, MaxExponent);

        // Computed in seconds as a double: a large base doubled that many times does not fit in a TimeSpan.
        var delaySeconds = Math.Min(
            policy.BackoffCap.TotalSeconds,
            policy.BackoffBase.TotalSeconds * Math.Pow(2, exponent));

        return utcNow.AddSeconds(delaySeconds);
    }
}
