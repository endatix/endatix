using Endatix.Framework.Modules;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Infrastructure.Tests.Data;

public class DbContextMigrationContributorTests
{
    [Fact]
    public async Task MigrateAsync_WhenShouldMigrateReturnsFalse_SkipsMigration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextMigrationContributor>(
            _ => new DbContextMigrationContributor<TestDbContext>(_ => false));
        var provider = services.BuildServiceProvider();

        // Act
        var contributor = provider.GetRequiredService<IDbContextMigrationContributor>();
        var action = () => contributor.MigrateAsync(provider, NullLogger.Instance);

        // Assert
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MigrateAsync_WhenDbContextNotRegistered_SkipsWithoutError()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextMigrationContributor>(
            _ => new DbContextMigrationContributor<TestDbContext>());
        var provider = services.BuildServiceProvider();

        // Act
        var contributor = provider.GetRequiredService<IDbContextMigrationContributor>();
        var action = () => contributor.MigrateAsync(provider, NullLogger.Instance);

        // Assert
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public void AddDbContextMigrationContributor_RegistersContributor()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDbContextMigrationContributor<TestDbContext>();
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetServices<IDbContextMigrationContributor>().Should().HaveCount(1);
    }

    private class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
    }
}
