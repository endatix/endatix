using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Endatix.IntegrationTests;

/// <summary>
/// The jobs meter is process-wide, so a test's recorder hears every host in the process. It reads only the nodes it
/// is asked about. Neither node starts or reaches a database.
/// </summary>
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("Lane", "Jobs")]
public sealed class JobsMeterRecorderTests
{
    [Fact]
    public async Task Misfire_counter_reads_only_the_node_that_misfired()
    {
        // Arrange
        using var misfires = JobMisfireCounter.Start();
        await using var quiet = UnstartedNode();
        await using var busy = UnstartedNode();

        // Act
        await MisfireAsync(busy, "TypeB");

        // Assert
        misfires.JobTypesOn(quiet).Should().BeEmpty();
        misfires.JobTypesOn(busy).Should().Equal("TypeB");
    }

    [Fact]
    public async Task Lifecycle_listener_reads_only_the_node_that_recorded()
    {
        // Arrange
        using var lifecycle = JobLifecycleEventListener.Start();
        await using var quiet = UnstartedNode();
        await using var busy = UnstartedNode();

        // Act
        busy.Services.GetRequiredService<IJobMetrics>().Record(JobLifecycleEvent.Enqueued, "TypeB");

        // Assert
        lifecycle.EventsOn(quiet).Should().BeEmpty();
        lifecycle.EventsOn(busy).Should().Equal(("enqueued", "TypeB"));
    }

    private static JobsTestNode UnstartedNode() =>
        JobsTestNode.Create(new JobsTestNodeDatabase(TestDatabaseProvider.PostgreSql, "Host=unreachable"), migrate: false);

    private static async Task MisfireAsync(JobsTestNode node, string jobType)
    {
        var listener = ActivatorUtilities.CreateInstance<JobMisfireListener>(node.Services);
        var trigger = TriggerBuilder.Create().ForJob(QuartzRegistration.JobKeyFor(jobType)).StartNow().Build();

        // The listener never reads the scheduler.
        await listener.TriggerMisfired(trigger, scheduler: null!, TestContext.Current.CancellationToken);
    }
}
