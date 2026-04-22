using Endatix.Core.Entities;
using Endatix.Infrastructure.Data.Config;
using Endatix.Infrastructure.Data.Config.AppEntities;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Tests.Data;

public class DataListsConfigurationTests
{
    [Fact]
    public void DataListConfiguration_MapsTenantAndNameUniqueIndex()
    {
        // Arrange
        ModelBuilder modelBuilder = new();
        DataListConfiguration configuration = new();

        // Act
        configuration.Configure(modelBuilder.Entity<DataList>());
        var entityType = modelBuilder.Model.FindEntityType(typeof(DataList));

        // Assert
        entityType.Should().NotBeNull();
        entityType!.GetIndexes()
            .Should()
            .Contain(index =>
                index.IsUnique &&
                index.Properties.Select(x => x.Name).SequenceEqual(new[] { "TenantId", "Name" }) &&
                index.GetFilter() == "\"IsDeleted\" = false");
    }

    [Fact]
    public void DataListConfiguration_WithSqlServerProvider_UsesBitZeroFilter()
    {
        ModelBuilder modelBuilder = new();
        DataListConfiguration configuration = new();
        configuration.SetDatabaseProviderName(EfCoreDatabaseProviders.SqlServer);

        configuration.Configure(modelBuilder.Entity<DataList>());
        var entityType = modelBuilder.Model.FindEntityType(typeof(DataList));

        entityType.Should().NotBeNull();
        entityType!.GetIndexes()
            .Should()
            .Contain(index =>
                index.IsUnique &&
                index.Properties.Select(x => x.Name).SequenceEqual(new[] { "TenantId", "Name" }) &&
                index.GetFilter() == "[IsDeleted] = 0");
    }

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
    public void FormDependencyConfiguration_MapsUniqueDependencyConstraint()
    {
        // Arrange
        ModelBuilder modelBuilder = new();
        FormDependencyConfiguration configuration = new();

        // Act
        configuration.Configure(modelBuilder.Entity<FormDependency>());
        var entityType = modelBuilder.Model.FindEntityType(typeof(FormDependency));

        // Assert
        entityType.Should().NotBeNull();
        entityType!.GetIndexes()
            .Should()
            .Contain(index =>
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
        var dependencyTypeProperty = entityType?.FindProperty("DependencyTypeIndex");

        // Assert
        dependencyTypeProperty.Should().NotBeNull();
        dependencyTypeProperty!.GetColumnName().Should().Be("DependencyType");
    }
}
