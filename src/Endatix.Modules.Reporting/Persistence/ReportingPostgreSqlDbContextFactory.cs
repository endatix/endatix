using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Endatix.Modules.Reporting.Persistence;

/// <summary>
/// Design-time factory for PostgreSQL migrations. Use with
/// <c>--startup-project src/Endatix.WebHost --context ReportingPostgreSqlDbContext</c>.
/// </summary>
/// <remarks>
/// Pins the provider rather than reading <c>DefaultConnection_DbProvider</c>, so the locally configured
/// provider cannot steer which chain a migration lands in. A missing <c>ConnectionStrings:DefaultConnection</c>
/// fails loudly.
/// </remarks>
public sealed class ReportingPostgreSqlDbContextFactory : IDesignTimeDbContextFactory<ReportingPostgreSqlDbContext>
{
    public ReportingPostgreSqlDbContext CreateDbContext(string[] args)
    {
        var configuration = ModuleDesignTimeConfiguration.Build();
        var connectionString = ModuleDesignTimeConfiguration.GetDefaultConnectionString(configuration);

        var optionsBuilder = new DbContextOptionsBuilder<ReportingPostgreSqlDbContext>();
        optionsBuilder.UseNpgsql(connectionString, dbOptions =>
        {
            dbOptions.MigrationsAssembly(ReportingPersistence.MigrationsAssembly);
            dbOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, ReportingPersistence.Schema);
        });

        return new ReportingPostgreSqlDbContext(
            optionsBuilder.Options,
            DesignTimeDbContextDependencies.TenantContext);
    }
}
