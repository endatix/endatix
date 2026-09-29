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
        var attemptOutcomes = Enum.GetNames<JobAttemptOutcome>();
        var methods = metricsType.GetMethods()
            .Select(method =>
                $"{method.Name}({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name))})")
            .ToList();

        // Assert
        lifecycleEvents.Should().BeEquivalentTo(
            "Enqueued",
            "Claimed",
            "Completed",
            "Failed",
            "RetryScheduled",
            "DeadLettered",
            "Canceled",
            "Abandoned");
        attemptOutcomes.Should().BeEquivalentTo(
            "Completed",
            "Failed",
            "RetryScheduled",
            "DeadLettered",
            "Canceled",
            "Abandoned");
        methods.Should().BeEquivalentTo(
            "Record(JobLifecycleEvent, String)",
            "ObserveDuration(String, TimeSpan, JobAttemptOutcome)");
    }
}
