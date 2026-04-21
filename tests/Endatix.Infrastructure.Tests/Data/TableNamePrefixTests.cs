using Endatix.Core.Configuration;
using Endatix.Infrastructure.Data;
using FluentAssertions;

namespace Endatix.Infrastructure.Tests.Data;

public class TableNamePrefixTests
{
    [Fact]
    public void GetTableName_WithDefaultEntityName_PluralizesName()
    {
        // Arrange
        var originalPrefix = EndatixConfig.Configuration.TablePrefix;
        EndatixConfig.Configuration.TablePrefix = null;

        try
        {
            // Act
            var tableName = TableNamePrefix.GetTableName("Endatix.Core.Entities.FormDependency");

            // Assert
            tableName.Should().Be("FormDependencys");
        }
        finally
        {
            EndatixConfig.Configuration.TablePrefix = originalPrefix;
        }
    }

    [Fact]
    public void GetTableName_WithExplicitConfiguredTableName_ReturnsConfiguredName()
    {
        // Arrange
        var originalPrefix = EndatixConfig.Configuration.TablePrefix;
        EndatixConfig.Configuration.TablePrefix = null;

        try
        {
            // Act
            var tableName = TableNamePrefix.GetTableName(
                "Endatix.Core.Entities.FormDependency",
                "FormDependencies");

            // Assert
            tableName.Should().Be("FormDependencies");
        }
        finally
        {
            EndatixConfig.Configuration.TablePrefix = originalPrefix;
        }
    }

    [Fact]
    public void GetTableName_WithPrefixAndDefaultName_AppliesPrefix()
    {
        // Arrange
        var originalPrefix = EndatixConfig.Configuration.TablePrefix;
        EndatixConfig.Configuration.TablePrefix = "custom";

        try
        {
            // Act
            var tableName = TableNamePrefix.GetTableName("Endatix.Core.Entities.Form");

            // Assert
            tableName.Should().Be("custom.Forms");
        }
        finally
        {
            EndatixConfig.Configuration.TablePrefix = originalPrefix;
        }
    }
}
