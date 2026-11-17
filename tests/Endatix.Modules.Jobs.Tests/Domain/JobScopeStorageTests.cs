using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Domain;

namespace Endatix.Modules.Jobs.Tests.Domain;

public class JobScopeStorageTests
{
    [Fact]
    public void ToStorage_ThenFromStorage_RoundTripsEachScope()
    {
        // Arrange
        JobScope[] scopes = [JobScope.System, JobScope.ForTenant(7)];

        // Act
        var roundTripped = scopes.Select(scope => JobScopeStorage.FromStorage(JobScopeStorage.ToStorage(scope)));

        // Assert
        roundTripped.Should().Equal(scopes);
    }

    [Fact]
    public void ToStorage_SystemAndTenant_StoreDistinctValues()
    {
        // Arrange
        // Act
        var system = JobScopeStorage.ToStorage(JobScope.System);
        var tenant = JobScopeStorage.ToStorage(JobScope.ForTenant(7));

        // Assert
        system.Should().Be(JobScopeStorage.SystemTenantId);
        tenant.Should().Be(7);
    }

    [Fact]
    public void FromStorage_NegativeValue_Throws()
    {
        // Arrange
        // Act
        var read = () => JobScopeStorage.FromStorage(-1);

        // Assert
        read.Should().Throw<InvalidOperationException>();
    }
}
