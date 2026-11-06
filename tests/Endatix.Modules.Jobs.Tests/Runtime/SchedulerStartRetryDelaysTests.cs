using Endatix.Modules.Jobs.Runtime;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class SchedulerStartRetryDelaysTests
{
    [Fact]
    public void Default_OnEveryHost_WaitsFiveSecondsAfterAnOutageAndOneAfterALostRace()
    {
        // Arrange — what every host retries a failed start with; only tests shorten it.

        // Act
        var delays = SchedulerStartRetryDelays.Default;

        // Assert
        delays.Unreachable.Should().Be(TimeSpan.FromSeconds(5));
        delays.Contended.Should().Be(TimeSpan.FromSeconds(1));
    }
}
