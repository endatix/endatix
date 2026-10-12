using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.IntegrationTests;

internal static class ReportingTestSchema
{
    public static async Task EnsureMigratedAsync(
        string connectionString,
        TestDatabaseProvider provider,
        CancellationToken cancellationToken = default)
    {
        await EnsureCoreMigratedAsync(connectionString, provider, cancellationToken);

        await using ReportingDbContextBase context = CreateContext(
            connectionString,
            provider,
            IntegrationTenantContext.Bypass);

        // Reporting integration tests reset data via Respawn but keep schema objects.
        // Drop the module schema so updated migrations (e.g. FormSchemas rename) apply cleanly.
        await context.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS reporting CASCADE;");
        await context.Database.MigrateAsync(cancellationToken);
    }

    /// <summary>
    /// Creates the Reporting context of the database provider under test, configured the way the host
    /// configures it.
    /// </summary>
    internal static ReportingDbContextBase CreateContext(
        string connectionString,
        TestDatabaseProvider provider,
        ITenantContext tenantContext)
    {
        var configuration = BuildTestConfiguration(connectionString, provider);
        return provider == TestDatabaseProvider.PostgreSql
            ? new ReportingPostgreSqlDbContext(HostOptions<ReportingPostgreSqlDbContext>(configuration), tenantContext)
            : new ReportingSqlServerDbContext(HostOptions<ReportingSqlServerDbContext>(configuration), tenantContext);
    }

    private static DbContextOptions<TContext> HostOptions<TContext>(IConfiguration configuration)
        where TContext : DbContext
    {
        DbContextOptionsBuilder<TContext> optionsBuilder = new();
        optionsBuilder.ConfigureModuleDbContext(configuration, ReportingPersistence.ConfigureDbContextOptions);
        return optionsBuilder.Options;
    }

    private static async Task EnsureCoreMigratedAsync(
        string connectionString,
        TestDatabaseProvider provider,
        CancellationToken cancellationToken)
    {
        IServiceProvider serviceProvider = IntegrationCoreMigrationTestHelper.BuildServiceProvider(
            connectionString,
            provider);

        await serviceProvider.ApplyDbMigrationsAsync(NullLogger.Instance, cancellationToken);
    }

    private static IConfiguration BuildTestConfiguration(string connectionString, TestDatabaseProvider provider) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["ConnectionStrings:DefaultConnection_DbProvider"] = provider.ToString()
            })
            .Build();
}
