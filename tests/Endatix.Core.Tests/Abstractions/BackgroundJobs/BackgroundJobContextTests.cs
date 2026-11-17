using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Core.Tests.Abstractions.BackgroundJobs;

public class BackgroundJobContextTests
{
    [Fact]
    public void TenantId_SystemJob_ThrowsScopeMismatch()
    {
        // Arrange — a handler written for tenant work fails rather than run with no tenant.
        var job = new BackgroundJobContext(1, "SignupWork", JobScope.System, "{}", 1);

        // Act
        var read = () => job.TenantId;

        // Assert
        read.Should().Throw<JobScopeMismatchException>()
            .Which.EndUserMessage.Should().Be(JobScopeMismatchException.WrongScope);
    }

    [Fact]
    public void ToString_SystemJob_PrintsTheScopeWithoutThrowing()
    {
        // Arrange
        var job = new BackgroundJobContext(1, "SignupWork", JobScope.System, "{}", 1);

        // Act
        var printed = job.ToString();

        // Assert
        printed.Should().Contain("Scope = System").And.NotContain("TenantId");
    }
}
