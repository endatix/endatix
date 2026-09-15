using Endatix.Modules.Jobs.Runtime;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>
/// Covers the retry backoff curve. The attempt count goes up when a job is claimed, so a first attempt that
/// fails is already attempt 1 and must wait exactly the base delay; these tests pin that offset together with the
/// doubling and the cap, up to an attempt count where uncapped doubling would overflow.
/// </summary>
public class BackgroundJobRetryPolicyTests
{
    private static readonly DateTime Now = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    [InlineData(6, 900)]
    [InlineData(40, 900)]
    public void NextAttemptAt_ClaimedAttempt_FollowsCappedCurve(int claimedAttempt, int expectedDelaySeconds)
    {
        // Arrange — the attempt budget and runtime ceiling play no part in the backoff curve.
        var policy = new BackgroundJobTypePolicy(
            MaxAttempts: 1,
            MaxRuntime: TimeSpan.FromMinutes(1),
            BackoffBase: TimeSpan.FromSeconds(30),
            BackoffCap: TimeSpan.FromSeconds(900));

        // Act
        var act = () => BackgroundJobRetryPolicy.NextAttemptAt(claimedAttempt, Now, policy);

        // Assert
        act.Should().NotThrow().Which.Should().Be(Now.AddSeconds(expectedDelaySeconds));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NextAttemptAt_AttemptBelowOne_WaitsBaseDelay(int claimedAttempt)
    {
        // Arrange
        var policy = new BackgroundJobTypePolicy(
            MaxAttempts: 1,
            MaxRuntime: TimeSpan.FromMinutes(1),
            BackoffBase: TimeSpan.FromSeconds(30),
            BackoffCap: TimeSpan.FromSeconds(900));

        // Act
        var act = () => BackgroundJobRetryPolicy.NextAttemptAt(claimedAttempt, Now, policy);

        // Assert — a negative exponent would wait less than the base delay, and int.MinValue minus one would wrap
        // round to the largest exponent.
        act.Should().NotThrow().Which.Should().Be(Now.AddSeconds(30));
    }

    [Theory]
    [InlineData(32)]
    [InlineData(int.MaxValue)]
    public void NextAttemptAt_OneSecondBaseAndLargestCap_ReachesCap(int claimedAttempt)
    {
        // Arrange — the smallest base with the largest cap the options accept needs the most doublings to reach it.
        var options = new BackgroundJobsOptions { BackoffBaseSeconds = 1, BackoffCapSeconds = int.MaxValue };
        var policy = options.ResolvePolicy("SubmissionExport");

        // Act
        var nextAttemptAt = BackgroundJobRetryPolicy.NextAttemptAt(claimedAttempt, Now, policy);

        // Assert
        nextAttemptAt.Should().Be(Now.AddSeconds(int.MaxValue));
    }
}
