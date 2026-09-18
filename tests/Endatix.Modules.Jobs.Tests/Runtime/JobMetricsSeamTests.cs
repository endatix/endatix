using Endatix.Modules.Jobs.Runtime;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public class JobMetricsSeamTests
{
    [Fact]
    public void IJobMetrics_PublicShape_MatchesDocumentedMembers()
    {
        // Arrange
        var metricsType = typeof(IJobMetrics);

        // Act
        var lifecycleEvents = Enum.GetNames<JobLifecycleEvent>();
        var methods = metricsType.GetMethods()
            .Select(method =>
                $"{method.Name}({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name))})")
            .ToList();

        // Assert — an exporter built against this assembly bakes in each event's value, so the order is part of the
        // contract.
        lifecycleEvents.Should().Equal(
            "Enqueued",
            "Claimed",
            "Completed",
            "Failed",
            "RetryScheduled",
            "DeadLettered",
            "Canceled",
            "Reaped",
            "OfferRejected",
            "Abandoned");
        methods.Should().BeEquivalentTo(
            "Record(JobLifecycleEvent, String)",
            "ObserveQueueDepth(Int32)",
            "ObserveBacklog(String, Int32, TimeSpan)",
            "ObserveDuration(String, TimeSpan, JobLifecycleEvent)");
    }
}
