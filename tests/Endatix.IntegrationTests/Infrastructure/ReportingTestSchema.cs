using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Endatix.IntegrationTests;

internal static class ReportingTestSchema
{
    public static async Task EnsureMigratedAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        var optionsBuilder = ConfigureOptionsBuilder(connectionString);

        await using ReportingDbContext context = new(
            optionsBuilder.Options,
            new NoOpIdGenerator(),
            new BypassTenantContext());

        await context.Database.MigrateAsync(cancellationToken);
    }

    internal static DbContextOptionsBuilder<ReportingDbContext> ConfigureOptionsBuilder(string connectionString)
    {
        var configuration = BuildTestConfiguration(connectionString);
        DbContextOptionsBuilder<ReportingDbContext> optionsBuilder = new();
        optionsBuilder.ConfigureModuleDbContext(configuration, ReportingPersistence.ConfigureDbContextOptions);
        return optionsBuilder;
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
