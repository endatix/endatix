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

        await using ReportingDbContext context = new(
            optionsBuilder.Options,
            new NoOpIdGenerator(),
            new BypassTenantContext());

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

    private sealed class NoOpIdGenerator : IIdGenerator<long>
    {
        public long CreateId() => 0;
    }

    private sealed class BypassTenantContext : ITenantContext
    {
        public long TenantId => 0;
    }
}
