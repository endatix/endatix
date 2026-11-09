using Endatix.Core.Abstractions;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Endatix.IntegrationTests;

/// <summary>The jobs test clock reaches what it stands in for on a real node: the scheduler and the row times.</summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class JobsTestClockTests(DbIntegrationFixture fixture)
{
    [Fact]
    public async Task Test_clock_is_the_schedulers_clock_and_the_hosts_now()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var clock = new JobsTestClock();
        await using var node = JobsTestNode.Create(database, JobsTestClock.LongRealTimeFallbacks.ToDictionary(), clock.Use);
        await node.StartAsync(ct);

        // Act
        var scheduler = await node.Services
            .GetRequiredKeyedService<ISchedulerFactory>(QuartzRegistration.SchedulerName)
            .GetScheduler(ct);
        await clock.StepUntilAsync(() => false, JobsTestClock.Step, ct);

        // Assert
        scheduler.TimeProvider.Should().BeSameAs(clock.Time);
        node.Services.GetRequiredService<IDateTimeProvider>().UtcNow.Should().Be(clock.Time.GetUtcNow());
    }
}
