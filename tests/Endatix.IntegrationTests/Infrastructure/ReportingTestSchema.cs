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

        var optionsBuilder = ConfigureOptionsBuilder(connectionString);

        SequentialIdGenerator idGenerator = new();

        await using ReportingDbContext context = new(
            optionsBuilder.Options,
            idGenerator,
            new BypassTenantContext(),
            new EfCoreValueGeneratorFactory(idGenerator));

        // Reporting integration tests reset data via Respawn but keep schema objects.
        // Drop the module schema so updated migrations (e.g. FormSchemas rename) apply cleanly.
        await context.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS reporting CASCADE;");
        await context.Database.MigrateAsync(cancellationToken);
    }

    internal static DbContextOptionsBuilder<ReportingDbContext> ConfigureOptionsBuilder(string connectionString)
    {
        var configuration = BuildTestConfiguration(connectionString);
        DbContextOptionsBuilder<ReportingDbContext> optionsBuilder = new();
        optionsBuilder.ConfigureModuleDbContext(configuration, ReportingPersistence.ConfigureDbContextOptions);
        return optionsBuilder;
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

    private static IConfiguration BuildTestConfiguration(string connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["ConnectionStrings:DefaultConnection_DbProvider"] = "PostgreSql"
            })
            .Build();

    /// <summary>
    /// Ids for the context that migrates the schema. Must be real values, not 0.
    /// </summary>
    /// <remarks>
    /// EF caches one model per context type, and the value generator configured in
    /// <c>ReportingDbContext.OnModelCreating</c> closes over the factory of whichever context built
    /// that model first — in this assembly, this one. A generator returning 0 here would hand every
    /// later test an Id of 0 and resurrect the duplicate-key failure this seeding path was fixed for.
    /// </remarks>
    private sealed class SequentialIdGenerator : IIdGenerator<long>
    {
        private long _current = 900_000;

        public long CreateId() => Interlocked.Increment(ref _current);
    }

    private sealed class BypassTenantContext : ITenantContext
    {
        public long TenantId => 0;
    }
}
