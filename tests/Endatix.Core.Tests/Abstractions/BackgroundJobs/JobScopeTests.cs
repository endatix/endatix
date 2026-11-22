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
    }

    [Fact]
    public void ForTenant_AnyTenant_IsNotSystem()
    {
        // Arrange
        // Act
        var scope = JobScope.ForTenant(7);

        // Assert
        scope.IsSystem.Should().BeFalse();
    }

    [Fact]
    public void System_IsEqualToItselfAndNotToATenant()
    {
        // Arrange
        // Act
        var scope = JobScope.System;

        // Assert
        scope.Should().Be(JobScope.System);
        scope.Should().NotBe(JobScope.ForTenant(7));
    }

    [Fact]
    public void ToString_NamesTheScope()
    {
        // Arrange
        // Act
        var printed = (System: JobScope.System.ToString(), Tenant: JobScope.ForTenant(5).ToString());

        // Assert
        printed.Should().Be(("System", "Tenant 5"));
    }
}
