using Endatix.Modules.Jobs.Endpoints;
using Endatix.Modules.Jobs.Runtime;

namespace Endatix.Modules.Jobs.Tests.Endpoints;

public sealed class QuartzDashboardTests
{
    private static readonly string JobClass = typeof(BackgroundJobExecution).FullName!;

    [Fact]
    public void IsEndatixJobClass_ExactOrAssemblyQualifiedName_IsAllowed()
    {
        // Arrange
        var names = new[] { JobClass, $"{JobClass}, {typeof(BackgroundJobExecution).Assembly.GetName().Name}" };

        // Act
        var allowed = names.Select(QuartzDashboard.IsEndatixJobClass);

        // Assert
        allowed.Should().AllSatisfy(result => result.Should().BeTrue());
    }

    [Theory]
    [InlineData("X")]
    [InlineData("+Nested")]
    [InlineData("X, Other.Assembly")]
    public void IsEndatixJobClass_NameThatOnlyStartsWithTheJobClass_IsRefused(string suffix)
    {
        // Arrange
        var name = JobClass + suffix;

        // Act
        var allowed = QuartzDashboard.IsEndatixJobClass(name);

        // Assert
        allowed.Should().BeFalse();
    }
}
