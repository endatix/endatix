using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity.Authentication;

namespace Endatix.Modules.Reporting.Persistence;

/// <summary>
/// Design-time factory for EF Core migrations.
/// </summary>
public sealed class ReportingDbContextFactory : IDesignTimeDbContextFactory<ReportingDbContext>
{
    public ReportingDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=endatix;Username=postgres;Password=postgres";

        var usePostgreSql = DatabaseProviderResolver.IsPostgreSql(configuration);
        var migrationsNamespace = usePostgreSql
            ? ReportingPersistence.PostgreSqlMigrationsNamespace
            : ReportingPersistence.SqlServerMigrationsNamespace;

        var optionsBuilder = new DbContextOptionsBuilder<ReportingDbContext>();

        if (usePostgreSql)
        {
            optionsBuilder.UseNpgsql(
                connectionString,
                dbOptions =>
                {
                    dbOptions.MigrationsAssembly(typeof(ReportingDbContext).Assembly.FullName);
                    dbOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, ReportingPersistence.Schema);
                });
        }
        else
        {
            optionsBuilder.UseSqlServer(
                connectionString,
                dbOptions =>
                {
                    dbOptions.MigrationsAssembly(typeof(ReportingDbContext).Assembly.FullName);
                    dbOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, ReportingPersistence.Schema);
                });
        }

        ModuleDbContextExtensions.ConfigureProviderScopedMigrations(optionsBuilder, migrationsNamespace);

        return new ReportingDbContext(
            optionsBuilder.Options,
            new DesignTimeIdGenerator(),
            new DesignTimeTenantContext());
    }

    private sealed class DesignTimeIdGenerator : IIdGenerator<long>
    {
        public long CreateId() => DateTime.UtcNow.Ticks;
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public long TenantId => AuthConstants.DEFAULT_TENANT_ID;
    }
}
