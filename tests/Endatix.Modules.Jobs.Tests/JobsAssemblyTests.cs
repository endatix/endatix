using Endatix.Modules.Jobs.Domain;

namespace Endatix.Modules.Jobs.Tests;

public sealed class JobsAssemblyTests
{
    [Fact]
    public void JobsAssembly_Types_ExcludeDispatchSeams()
    {
        // Arrange — the scheduler dispatches and detects dead nodes, so the home-built seams must not creep back.
        var assembly = typeof(JobsModule).Assembly;
        string[] removedTypes = ["IJobDispatchStrategy", "SinglePoolDispatchStrategy", "JobDispatchItem"];

        // Act
        var typeNames = assembly.GetTypes().Select(type => type.Name).ToList();
        var jobMembers = typeof(BackgroundJob).GetMembers().Select(member => member.Name).ToList();

        // Assert
        typeNames.Should().NotContain(removedTypes);
        jobMembers.Should().NotContain("HeartbeatAt");
    }
}
