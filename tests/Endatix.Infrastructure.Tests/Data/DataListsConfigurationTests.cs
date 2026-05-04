using Endatix.Core.Entities;
using Endatix.Infrastructure.Data.Config.AppEntities;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Tests.Data;

public class DataListsConfigurationTests
{
    [Fact]
    public void DataListItemConfiguration_MapsDataListIndex()
    {
        // Arrange
        ModelBuilder modelBuilder = new();
        DataListItemConfiguration configuration = new();

        // Act
        configuration.Configure(modelBuilder.Entity<DataListItem>());
        var entityType = modelBuilder.Model.FindEntityType(typeof(DataListItem));

        // Assert
        entityType.Should().NotBeNull();
        entityType!.GetIndexes()
            .Should()
            .Contain(index =>
                index.Properties.Select(x => x.Name).SequenceEqual(new[] { "DataListId" }));
    }

    [Fact]
    public void FormDependencyConfiguration_UniqueConstraintIsProviderSpecific_NotOnBaseModel()
    {
        // Unique filtered index is on FormDependencyConfigurationSqlServer / FormDependencyConfigurationPostgreSql (see DataList pattern).
        ModelBuilder modelBuilder = new();
        FormDependencyConfiguration configuration = new();

        configuration.Configure(modelBuilder.Entity<FormDependency>());
        var entityType = modelBuilder.Model.FindEntityType(typeof(FormDependency));

        entityType.Should().NotBeNull();
        entityType!.GetIndexes()
            .Should()
            .NotContain(index =>
                index.IsUnique &&
                index.Properties.Select(x => x.Name).SequenceEqual(new[] { "FormId", "DependencyTypeIndex", "DependencyIdentifier" }));
    }

    [Fact]
    public void FormDependencyConfiguration_MapsDependencyTypeCodeProperty()
    {
        // Arrange
        ModelBuilder modelBuilder = new();
        FormDependencyConfiguration configuration = new();

        // Act
        configuration.Configure(modelBuilder.Entity<FormDependency>());
        var entityType = modelBuilder.Model.FindEntityType(typeof(FormDependency));
        var dependencyTypeProperty = entityType?.FindProperty(FormDependencyConfiguration.DependencyTypeIndexPropertyName);

        // Assert
        dependencyTypeProperty.Should().NotBeNull();
        dependencyTypeProperty!.GetColumnName().Should().Be("DependencyType");
    }
}
