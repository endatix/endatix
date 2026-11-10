using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.IntegrationTests;

/// <summary>
/// Whether the scheduler's firing rounds take the trigger lock, as the store decides it at startup.
/// </summary>
/// <remarks>
/// A round that takes no lock reads its triggers through members of Quartz's own delegates that bypass the
/// Endatix delegates' <c>SelectTriggers</c>, so recovered triggers would not count against their job type's cap.
/// Quartz runs such rounds only through the delegates it ships; the store's own startup line is the verdict.
/// </remarks>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("Lane", "Jobs")]
public sealed class JobsLockFreeRoundsTests(DbIntegrationFixture fixture)
{
    // Quartz's event 3056, with the reason it gives for a delegate of its own that it did not ship.
    private const string RoundsOffForTheDelegate =
        "Lock-free firing rounds are off: the driver delegate does not take part in fire-on-acquire rounds";

    [Fact]
    public async Task SchedulerStart_EndatixDriverDelegate_TakesTheTriggerLockEveryRound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var logs = new CapturingLoggerProvider(LogLevel.Information);
        await using var node = JobsTestNode.Create(
            database,
            configureServices: services =>
            {
                // The test host keeps Quartz below Warning out of its providers; the verdict is logged at Information.
                services.AddLogging(logging => logging.AddFilter<CapturingLoggerProvider>("Quartz", LogLevel.Information));
                services.AddSingleton<ILoggerProvider>(logs);
            });

        // Act
        await node.StartAsync(ct);

        // Assert
        logs.Entries.Should().Contain(entry => entry.Message.Contains(RoundsOffForTheDelegate));
    }
}
