using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Core.Tests.Abstractions.BackgroundJobs;

public class JobScopeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ForTenant_ZeroOrNegativeTenant_Throws(long tenantId)
    {
        // Arrange
        // Act
        var create = () => JobScope.ForTenant(tenantId);

        // Assert
        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void System_HasNoTenant()
    {
        // Arrange
        // Act
        var scope = JobScope.System;

        // Assert
        scope.IsSystem.Should().BeTrue();
        scope.TenantId.Should().BeNull();
    }

    [Fact]
    public void ForTenant_SameTenant_IsEqual()
    {
        // Arrange
        // Act
        var scope = JobScope.ForTenant(7);

        // Assert
        scope.Should().Be(JobScope.ForTenant(7));
        scope.Should().NotBe(JobScope.System);
        scope.IsSystem.Should().BeFalse();
    }

    [Fact]
    public void BackgroundJobRequestCreate_TenantZero_Throws()
    {
        // Arrange
        // Act
        var create = () => BackgroundJobRequest.Create(new TestPayload(1), tenantId: 0);

        // Assert
        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BackgroundJobContextTenantId_SystemJob_Throws()
    {
        // Arrange — a handler written for tenant work fails rather than run with no tenant.
        var job = new BackgroundJobContext(1, "SignupWork", JobScope.System, "{}", 1);

        // Act
        var read = () => job.TenantId;

        // Assert
        read.Should().Throw<InvalidOperationException>();
    }
}
