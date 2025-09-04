using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using FluentAssertions;
using System.Reflection;

namespace Endatix.Infrastructure.Tests.Data;

public class DbContextModelBuilderExtensionsTests
{
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
