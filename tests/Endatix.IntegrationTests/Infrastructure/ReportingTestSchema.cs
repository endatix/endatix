using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Locking;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Persistence;
using Endatix.Persistence.PostgreSql.Locking;
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
        await using ReportingDbContextBase context = await DropSchemaAsync(connectionString, provider, cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
    }

    /// <summary>
    /// Migrates the core schema and drops the Reporting one, returning a context to migrate it with, so a test can
    /// choose how far the Reporting chain goes.
    /// </summary>
    internal static async Task<ReportingDbContextBase> DropSchemaAsync(
        string connectionString,
        TestDatabaseProvider provider,
        CancellationToken cancellationToken)
    {
        RequirePostgreSql(provider);
        await EnsureCoreMigratedAsync(connectionString, provider, cancellationToken);

        var context = CreateContext(connectionString, provider, IntegrationTenantContext.Bypass);

        try
        {
            // Reporting integration tests reset data via Respawn but keep schema objects.
            // Drop the module schema so updated migrations (e.g. FormSchemas rename) apply cleanly.
            await context.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS reporting CASCADE;", cancellationToken);
            return context;
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
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
    /// The transaction lock a PostgreSQL host registers (<c>TransactionLockTests</c> pins that registration).
    /// </summary>
    /// <exception cref="NotSupportedException"><paramref name="context"/> is not on PostgreSQL.</exception>
    internal static ITransactionLock TransactionLockFor(IReportingDbContext context)
    {
        if (!context.Database.IsNpgsql())
        {
            throw new NotSupportedException(
                "Reporting integration tests lock schema rebuilds on PostgreSQL only, not on " +
                $"'{context.Database.ProviderName}'.");
        }

        return new PostgreSqlTransactionLock();
    }

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
