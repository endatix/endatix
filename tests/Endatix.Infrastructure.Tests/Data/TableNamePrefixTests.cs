using Endatix.Infrastructure.Data;

namespace Endatix.Infrastructure.Tests.Data;

public class TableNamePrefixTests
{
    [Fact]
    public void GetTableName_WithDefaultEntityName_PluralizesName()
    {
        var tableName = TableNamePrefix.GetTableName("Endatix.Core.Entities.FormDependency");

        tableName.Should().Be("FormDependencys");
    }

    [Fact]
    public void GetTableName_WithExplicitConfiguredTableName_ReturnsConfiguredName()
    {
        var tableName = TableNamePrefix.GetTableName(
            "Endatix.Core.Entities.FormDependency",
            "FormDependencies");

        tableName.Should().Be("FormDependencies");
    }
}
