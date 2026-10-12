using Endatix.Infrastructure.Data;
using Endatix.Modules.Jobs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Endatix.Modules.Jobs.Tests.Persistence;

/// <summary>
/// Which migrations each provider's context finds. Both chains live in one assembly, so a migration bound to the
/// wrong context would run against the other provider. The contexts are built the way the module registers them;
/// no connection is opened.
/// </summary>
public sealed class JobsMigrationsAvailabilityTests
{
    [Fact]
    public void GetMigrations_PostgreSqlContext_ReturnsTheThreeShippedIds()
    {
        // Arrange — databases have applied these ids; any other id would run again or be skipped.
        using var context = new JobsPostgreSqlDbContext(
            OptionsFor<JobsPostgreSqlDbContext>("postgresql", "Host=127.0.0.1;Database=not_connected;Username=x;Password=x"),
            DesignTimeDbContextDependencies.TenantContext);

        // Act
        var migrations = context.Database.GetMigrations();

        // Assert
        migrations.Should().Equal(
            "20260928150416_InitialBackgroundJobs",
            "20260928153000_AddTriggerAcquisitionIndex",
            "20260928161206_AddDedupKeyToBackgroundJobs");
    }

    [Fact]
    public void GetMigrations_SqlServerContext_ReturnsOnlyItsInitialMigration()
    {
        // Arrange — none of the PostgreSQL chain, which shares the assembly.
        using var context = CreateSqlServerContext();

        // Act
        var migrations = context.Database.GetMigrations();

        // Assert
        migrations.Should().Equal("20261009151311_InitialBackgroundJobs");
    }

    [Fact]
    public void HasPendingModelChanges_SqlServerContext_ReturnsFalse()
    {
        // Arrange
        using var context = CreateSqlServerContext();

        // Act
        var hasPendingModelChanges = context.Database.HasPendingModelChanges();

        // Assert
        hasPendingModelChanges.Should().BeFalse("the SQL Server snapshot must match the model the context builds");
    }

    private static JobsSqlServerDbContext CreateSqlServerContext() =>
        new(
            OptionsFor<JobsSqlServerDbContext>("sqlserver", "Server=127.0.0.1;Database=not_connected;User Id=x;Password=x"),
            DesignTimeDbContextDependencies.TenantContext);

    private static DbContextOptions<TContext> OptionsFor<TContext>(string provider, string connectionString)
        where TContext : DbContext
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["ConnectionStrings:DefaultConnection_DbProvider"] = provider,
            })
            .Build();
        var options = new DbContextOptionsBuilder<TContext>();
        options.ConfigureModuleDbContext(configuration, JobsPersistence.ConfigureDbContextOptions);
        return options.Options;
    }
}
