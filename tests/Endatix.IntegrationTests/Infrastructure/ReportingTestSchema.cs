using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Locking;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Persistence;
using Endatix.Persistence.PostgreSql.Locking;
using Endatix.Persistence.SqlServer.Locking;
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
        RequirePostgreSql(provider);
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
    /// <exception cref="NotSupportedException">The provider is SQL Server, which has no Reporting migrations yet.</exception>
    internal static ReportingDbContextBase CreateContext(
        string connectionString,
        TestDatabaseProvider provider,
        ITenantContext tenantContext)
    {
        RequirePostgreSql(provider);
        var configuration = BuildTestConfiguration(connectionString, provider);
        return new ReportingPostgreSqlDbContext(HostOptions<ReportingPostgreSqlDbContext>(configuration), tenantContext);
    }

    /// <summary>
    /// The transaction lock the host registers for the database <paramref name="context"/> is connected to.
    /// </summary>
    internal static ITransactionLock TransactionLockFor(IReportingDbContext context) =>
        context.Database.IsNpgsql() ? new PostgreSqlTransactionLock() : new SqlServerTransactionLock();

    // The schema reset in EnsureMigratedAsync is PostgreSQL SQL, and a SQL Server context would migrate an empty
    // chain, so a test that runs on SQL Server fails here, naming the reason, rather than on unrelated syntax.
    private static void RequirePostgreSql(TestDatabaseProvider provider)
    {
        if (provider != TestDatabaseProvider.PostgreSql)
        {
            throw new NotSupportedException(
                "Reporting has no SQL Server migrations yet, so Reporting integration tests run on PostgreSQL only.");
        }
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
