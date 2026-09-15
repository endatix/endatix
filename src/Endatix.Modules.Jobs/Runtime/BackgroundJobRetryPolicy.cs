namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Decides when a job whose attempt failed retryably becomes eligible again.
/// </summary>
internal static class BackgroundJobRetryPolicy
{
    // Bounds the doubling so the arithmetic stays finite for any attempt count. At this exponent even a
    // one-second base already exceeds thirty years, past any cap worth configuring.
    private const int MaxExponent = 30;

    /// <summary>
    /// Returns <paramref name="utcNow"/> plus the retry delay for <paramref name="claimedAttempt"/>: the policy's
    /// backoff base, doubled once for every attempt before this one, and never longer than its backoff cap.
    /// </summary>
    /// <param name="claimedAttempt">The attempt count the job row held once this attempt was claimed.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <param name="policy">The resolved policy for the job's type.</param>
    /// <remarks>
    /// The exponent is the claimed attempt minus one. The attempt count goes up when a job is claimed, not when
    /// it fails, so a first attempt that fails is already attempt 1 and has to wait exactly the base delay.
    /// </remarks>
    public static DateTime NextAttemptAt(int claimedAttempt, DateTime utcNow, BackgroundJobTypePolicy policy)
    {
        var exponent = Math.Min(claimedAttempt - 1, MaxExponent);

        // Computed in seconds as a double: a large base doubled thirty times does not fit in a TimeSpan.
        var delaySeconds = Math.Min(
            policy.BackoffCap.TotalSeconds,
            policy.BackoffBase.TotalSeconds * Math.Pow(2, exponent));

        return utcNow.AddSeconds(delaySeconds);
    }
}
