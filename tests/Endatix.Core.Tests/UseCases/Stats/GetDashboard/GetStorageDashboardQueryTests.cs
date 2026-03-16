using Endatix.Core.UseCases.Stats.GetDashboard;

namespace Endatix.Core.Tests.UseCases.Stats.GetDashboard;

public class GetStorageDashboardQueryTests
{
    [Fact]
    public void Constructor_WithTenantId_SetsPropertyCorrectly()
    {
        // Arrange
        var tenantId = SampleData.TENANT_ID;

        // Act
        var query = new GetStorageDashboardQuery(tenantId);

        // Assert
        query.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void Constructor_WithNullTenantId_SetsPropertyCorrectly()
    {
        // Arrange & Act
        var query = new GetStorageDashboardQuery(null);

        // Assert
        query.TenantId.Should().BeNull();
    }
}
