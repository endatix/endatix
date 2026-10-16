using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Locking;
using Endatix.IntegrationTests.Shared;
using Endatix.Core.Infrastructure;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Modules.Reporting.Persistence;
using Endatix.Persistence.PostgreSql.Locking;
using Endatix.Persistence.PostgreSql.Querying;
using Endatix.Persistence.SqlServer.Locking;
using Endatix.Persistence.SqlServer.Querying;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.IntegrationTests;

internal static class ReportingTestSchema
{
    // SQL Server has no DROP SCHEMA … CASCADE: the tables go first, the mappings before the formats they reference.
    private const string DropSqlServerSchemaSql = """
        DROP TABLE IF EXISTS [reporting].[SurveyTypeExportMappings];
        DROP TABLE IF EXISTS [reporting].[ExportFormats];
        DROP TABLE IF EXISTS [reporting].[FlattenedSubmissions];
        DROP TABLE IF EXISTS [reporting].[FormSchemas];
        DROP TABLE IF EXISTS [reporting].[__EFMigrationsHistory];
        DROP SCHEMA IF EXISTS [reporting];
        """;

    private const string DropPostgreSqlSchemaSql = "DROP SCHEMA IF EXISTS reporting CASCADE;";

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
        await EnsureCoreMigratedAsync(connectionString, provider, cancellationToken);

        var context = CreateContext(connectionString, provider, IntegrationTenantContext.Bypass);

        try
        {
            // Reporting integration tests reset data via Respawn but keep schema objects.
            // Drop the module schema so updated migrations (e.g. FormSchemas rename) apply cleanly.
            await context.Database.ExecuteSqlRawAsync(
                provider == TestDatabaseProvider.SqlServer ? DropSqlServerSchemaSql : DropPostgreSqlSchemaSql,
                cancellationToken);
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
    internal static ReportingDbContextBase CreateContext(
        string connectionString,
        TestDatabaseProvider provider,
        ITenantContext tenantContext)
    {
        var configuration = BuildTestConfiguration(connectionString, provider);
        return provider == TestDatabaseProvider.SqlServer
            ? new ReportingSqlServerDbContext(HostOptions<ReportingSqlServerDbContext>(configuration), tenantContext)
            : new ReportingPostgreSqlDbContext(HostOptions<ReportingPostgreSqlDbContext>(configuration), tenantContext);
    }

    /// <summary>
    /// The transaction lock a host on the context's provider registers (<c>TransactionLockTests</c> pins that
    /// registration).
    /// </summary>
    internal static ITransactionLock TransactionLockFor(IReportingDbContext context) =>
        context.Database.IsSqlServer() ? new SqlServerTransactionLock() : new PostgreSqlTransactionLock();

    /// <summary>
    /// The search copy on the context, as the host registers it. Rebuild requests go to
    /// <paramref name="outboxMessages"/>; a schema compile never makes one, so it may be left out.
    /// </summary>
    internal static FormSearchCopy SearchCopyFor(
        ReportingDbContextBase context,
        IRepository<OutboxMessage>? outboxMessages = null) =>
        new(
            new DateTimeProvider(),
            SearchCopyRebuilderFor(context),
            new SearchCopyRebuildRequests(outboxMessages ?? Substitute.For<IRepository<OutboxMessage>>(), new DateTimeProvider()));

    /// <summary>The rebuild of search copies on the context, as the host registers it.</summary>
    internal static SearchCopyRebuilder SearchCopyRebuilderFor(ReportingDbContextBase context) =>
        new(
            context,
            new FormSchemaRepository(context, new ReportingUnitOfWork(context), TransactionLockFor(context)),
            NullLogger<SearchCopyRebuilder>.Instance);

    /// <summary>
    /// Runs the frozen seed of the context's provider again, the SQL its first seeding migration ran.
    /// </summary>
    internal static Task RunFrozenSeedAsync(ReportingDbContextBase context, CancellationToken cancellationToken)
    {
        var seed = context.Database.IsSqlServer() ? SeedDefaultExportFormatsSqlServerSql.Up : SeedDefaultExportFormatsSql.Up;

        // Raw SQL is run as a format string, so the braces of its JSON literals are escaped.
        return context.Database.ExecuteSqlRawAsync(seed.Replace("{", "{{").Replace("}", "}}"), cancellationToken);
    }

    private static DbContextOptions<TContext> HostOptions<TContext>(IConfiguration configuration)
        where TContext : DbContext
    {
        DbContextOptionsBuilder<TContext> optionsBuilder = new();
        optionsBuilder.ConfigureModuleDbContext(configuration, ReportingPersistence.ConfigureDbContextOptions);

        // What the persistence builder's IModuleDbContextProviderOptions applies in a host: the JSON database functions.
        if (DatabaseProviderResolver.IsPostgreSql(configuration))
        {
            optionsBuilder.ReplaceService<IModelCustomizer, NpgsqlEndatixModelCustomizer>();
        }
        else
        {
            optionsBuilder.ReplaceService<IModelCustomizer, SqlServerEndatixModelCustomizer>();
        }

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
