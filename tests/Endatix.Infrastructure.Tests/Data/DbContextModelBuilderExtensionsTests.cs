using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.SqlServer.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using FluentAssertions;
using System.Reflection;

namespace Endatix.Infrastructure.Tests.Data;

public class DbContextModelBuilderExtensionsTests
{
    private const string NpgsqlStrategyAnnotation = "Npgsql:ValueGenerationStrategy";
    private const string SqlServerStrategyAnnotation = "SqlServer:ValueGenerationStrategy";
    private const string NotConnectedNpgsql = "Host=127.0.0.1;Database=__model_only_not_connected__";
    private const string NotConnectedSqlServer = "Server=127.0.0.1;Database=__model_only_not_connected__";

    [Fact]
    public void ApplyConfigurationsFor_WithValidAttribute_AppliesOnlyMatchingConfigurations()
    {
        // Arrange
        var builder = new ModelBuilder();
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        builder.ApplyConfigurationsFor<FooDbContext>(assembly);

        // Assert
        // Verify that only configurations with [ApplyConfigurationFor<TestDbContext>] are applied
        var entityTypes = builder.Model.GetEntityTypes();
        entityTypes.Should().Contain(et => et.ClrType == typeof(AlphaEntity));
        entityTypes.Should().NotContain(et => et.ClrType == typeof(BetaEntity));
    }

    [Fact]
    public void ApplyConfigurationsFor_WithDifferentDbContext_AppliesOnlyMatchingConfigurations()
    {
        // Arrange
        var builder = new ModelBuilder();
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        builder.ApplyConfigurationsFor<BarDbContext>(assembly);

        // Assert
        // Verify that only configurations with [ApplyConfigurationFor<OtherTestDbContext>] are applied
        var entityTypes = builder.Model.GetEntityTypes();
        entityTypes.Should().NotContain(et => et.ClrType == typeof(AlphaEntity));
        entityTypes.Should().Contain(et => et.ClrType == typeof(BetaEntity));
    }

    [Fact]
    public void ApplyConfigurationsFor_WithNoMatchingConfigurations_DoesNotApplyAnyConfigurations()
    {
        // Arrange
        var builder = new ModelBuilder();
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        builder.ApplyConfigurationsFor<NonExistentDbContext>(assembly);

        // Assert
        var entityTypes = builder.Model.GetEntityTypes();
        entityTypes.Should().BeEmpty();
    }

    [Fact]
    public void ApplyConfigurationsFor_IgnoresAbstractClasses()
    {
        // Arrange
        var builder = new ModelBuilder();
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        builder.ApplyConfigurationsFor<FooDbContext>(assembly);

        // Assert
        // Abstract configuration should not be applied
        var entityTypes = builder.Model.GetEntityTypes();
        entityTypes.Should().NotContain(et => et.ClrType == typeof(DeltaEntity));
    }

    [Fact]
    public void ApplyConfigurationsFor_IgnoresNonConfigurationClasses()
    {
        // Arrange
        var builder = new ModelBuilder();
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        builder.ApplyConfigurationsFor<FooDbContext>(assembly);

        // Assert
        // Non-configuration class should not be applied
        var entityTypes = builder.Model.GetEntityTypes();
        entityTypes.Should().NotContain(et => et.ClrType == typeof(NonConfigurationClass));
    }

    [Fact]
    public void ApplyConfigurationsFor_WithMultipleConfigurationsForSameDbContext_AppliesAllMatchingConfigurations()
    {
        // Arrange
        var builder = new ModelBuilder();
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        builder.ApplyConfigurationsFor<FooDbContext>(assembly);

        // Assert
        // Both configurations for TestDbContext should be applied
        var entityTypes = builder.Model.GetEntityTypes();
        entityTypes.Should().Contain(et => et.ClrType == typeof(AlphaEntity));
        entityTypes.Should().Contain(et => et.ClrType == typeof(GamaEntity));
    }

    [Fact]
    public void ApplyConfigurationsFor_WithNullAssembly_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new ModelBuilder();

        // Act & Assert
        var action = () => builder.ApplyConfigurationsFor<FooDbContext>(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplySnowflakeIdValueGenerators_NpgsqlModel_SetsOnlyNpgsqlStrategy()
    {
        // Arrange
        using var context = new ProviderScopedSnowflakeDbContext(
            new DbContextOptionsBuilder<ProviderScopedSnowflakeDbContext>().UseNpgsql(NotConnectedNpgsql).Options);

        // Act
        var id = IdPropertyOf(context);

        // Assert
        id.FindAnnotation(NpgsqlStrategyAnnotation)!.Value.Should().Be(NpgsqlValueGenerationStrategy.None);
        id.FindAnnotation(SqlServerStrategyAnnotation).Should().BeNull();
        id.ValueGenerated.Should().Be(ValueGenerated.OnAdd);
        id.GetValueGeneratorFactory().Should().NotBeNull();
    }

    [Fact]
    public void ApplySnowflakeIdValueGenerators_SqlServerModel_SetsOnlySqlServerStrategy()
    {
        // Arrange
        using var context = new ProviderScopedSnowflakeDbContext(
            new DbContextOptionsBuilder<ProviderScopedSnowflakeDbContext>().UseSqlServer(NotConnectedSqlServer).Options);

        // Act
        var id = IdPropertyOf(context);

        // Assert
        id.FindAnnotation(SqlServerStrategyAnnotation)!.Value.Should().Be(SqlServerValueGenerationStrategy.None);
        id.FindAnnotation(NpgsqlStrategyAnnotation).Should().BeNull();
        id.ValueGenerated.Should().Be(ValueGenerated.OnAdd);
        id.GetValueGeneratorFactory().Should().NotBeNull();
    }

    [Fact]
    public void ApplySnowflakeIdValueGenerators_InMemoryModel_SetsNoProviderStrategy()
    {
        // Arrange
        using var context = new ProviderScopedSnowflakeDbContext(
            new DbContextOptionsBuilder<ProviderScopedSnowflakeDbContext>()
                .UseInMemoryDatabase(nameof(ApplySnowflakeIdValueGenerators_InMemoryModel_SetsNoProviderStrategy))
                .Options);

        // Act
        var buildModel = () => IdPropertyOf(context);

        // Assert
        var id = buildModel.Should().NotThrow().Subject;
        id.FindAnnotation(NpgsqlStrategyAnnotation).Should().BeNull();
        id.FindAnnotation(SqlServerStrategyAnnotation).Should().BeNull();
        id.GetValueGeneratorFactory().Should().NotBeNull();
    }

    [Fact]
    public void ApplySnowflakeIdValueGenerators_ParameterlessOverload_WritesBothStrategiesAndIsObsolete()
    {
        // Arrange
        using var context = new BothProvidersSnowflakeDbContext(
            new DbContextOptionsBuilder<BothProvidersSnowflakeDbContext>().UseNpgsql(NotConnectedNpgsql).Options);
        var parameterlessOverload = typeof(DbContextModelBuilderExtensions).GetMethod(
            nameof(DbContextModelBuilderExtensions.ApplySnowflakeIdValueGenerators),
            [typeof(ModelBuilder)]);

        // Act
        var id = IdPropertyOf(context);

        // Assert
        id.FindAnnotation(NpgsqlStrategyAnnotation)!.Value.Should().Be(NpgsqlValueGenerationStrategy.None);
        id.FindAnnotation(SqlServerStrategyAnnotation)!.Value.Should().Be(SqlServerValueGenerationStrategy.None);
        parameterlessOverload!.GetCustomAttribute<ObsoleteAttribute>()!.Message.Should().Contain(nameof(DatabaseFacade));
    }

    // The design-time model is the one migrations scaffold snapshots from, so annotations are read there.
    private static IReadOnlyProperty IdPropertyOf(DbContext context) =>
        context.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(AlphaEntity))!
            .FindProperty(nameof(AlphaEntity.Id))!;
}

// Test entities
public class AlphaEntity
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class BetaEntity
{
    public long Id { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class GamaEntity
{
    public long Id { get; set; }
    public string Value { get; set; } = string.Empty;
}

public class DeltaEntity
{
    public long Id { get; set; }
}

// Test DbContexts
public class FooDbContext : DbContext
{
    public FooDbContext(DbContextOptions<FooDbContext> options) : base(options) { }
}

public class BarDbContext : DbContext
{
    public BarDbContext(DbContextOptions<BarDbContext> options) : base(options) { }
}

public class NonExistentDbContext : DbContext
{
    public NonExistentDbContext(DbContextOptions<NonExistentDbContext> options) : base(options) { }
}

public class ProviderScopedSnowflakeDbContext(DbContextOptions<ProviderScopedSnowflakeDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AlphaEntity>();
        modelBuilder.ApplySnowflakeIdValueGenerators(Database);
    }
}

// A separate context type, because EF caches one model per context type and provider.
public class BothProvidersSnowflakeDbContext(DbContextOptions<BothProvidersSnowflakeDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AlphaEntity>();
#pragma warning disable CS0618 // The obsolete overload is the subject of the test that uses this context.
        modelBuilder.ApplySnowflakeIdValueGenerators();
#pragma warning restore CS0618
    }
}

// Test configurations
[ApplyConfigurationFor<FooDbContext>]
public class AlphaEntityConfiguration : IEntityTypeConfiguration<AlphaEntity>
{
    public void Configure(EntityTypeBuilder<AlphaEntity> builder)
    {
        builder.ToTable("TestEntities");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100);
    }
}

[ApplyConfigurationFor<BarDbContext>]
public class BetaEntityConfiguration : IEntityTypeConfiguration<BetaEntity>
{
    public void Configure(EntityTypeBuilder<BetaEntity> builder)
    {
        builder.ToTable("OtherTestEntities");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Description).HasMaxLength(200);
    }
}

[ApplyConfigurationFor<FooDbContext>]
public class GamaEntityConfiguration : IEntityTypeConfiguration<GamaEntity>
{
    public void Configure(EntityTypeBuilder<GamaEntity> builder)
    {
        builder.ToTable("AnotherTestEntities");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Value).HasMaxLength(150);
    }
}

[ApplyConfigurationFor<FooDbContext>]
public abstract class DeltaEntityConfiguration : IEntityTypeConfiguration<DeltaEntity>
{
    public void Configure(EntityTypeBuilder<DeltaEntity> builder)
    {
        builder.ToTable("AbstractTestEntities");
        builder.HasKey(e => e.Id);
    }
}

// Non-configuration class with attribute (should be ignored)
[ApplyConfigurationFor<FooDbContext>]
public class NonConfigurationClass
{
    public string SomeProperty { get; set; } = string.Empty;
}
