using Endatix.Modules.Jobs.Runtime;

namespace Endatix.Modules.Jobs.Tests.Runtime;

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

        // Assert — the attempt count goes up at the claim, so attempt 1 is the first failure and waits the base
        // delay, and an attempt far past the cap still resolves to the cap.
        act.Should().NotThrow().Which.Should().Be(Now.AddSeconds(expectedDelaySeconds));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NextAttemptAt_AttemptBelowOne_WaitsBaseDelay(int claimedAttempt)
    {
        // Arrange
        var policy = new BackgroundJobTypePolicy(
            MaxAttempts: 1,
            MaxRuntime: TimeSpan.FromMinutes(1),
            BackoffBase: TimeSpan.FromSeconds(30),
            BackoffCap: TimeSpan.FromSeconds(900));

        // Act
        var nextAttemptAt = BackgroundJobRetryPolicy.NextAttemptAt(claimedAttempt, Now, policy);

        // Assert — a negative exponent would wait less than the base delay.
        nextAttemptAt.Should().Be(Now.AddSeconds(30));
    }
}
