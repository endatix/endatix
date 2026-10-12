using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Endatix.Modules.Reporting.Persistence;

/// <summary>
/// Design-time factory for SQL Server migrations. Use with
/// <c>--startup-project src/Endatix.WebHost --context ReportingSqlServerDbContext</c> and a SQL Server
/// <c>ConnectionStrings:DefaultConnection</c>.
/// </summary>
/// <remarks>
/// Pins the provider rather than reading <c>DefaultConnection_DbProvider</c>, so the locally configured
/// provider cannot steer which chain a migration lands in. A missing <c>ConnectionStrings:DefaultConnection</c>
/// fails loudly.
/// </remarks>
public sealed class ReportingSqlServerDbContextFactory : IDesignTimeDbContextFactory<ReportingSqlServerDbContext>
{
    public ReportingSqlServerDbContext CreateDbContext(string[] args)
    {
        var configuration = ModuleDesignTimeConfiguration.Build();
        var connectionString = ModuleDesignTimeConfiguration.GetDefaultConnectionString(configuration);

        var optionsBuilder = new DbContextOptionsBuilder<ReportingSqlServerDbContext>();
        optionsBuilder.UseSqlServer(connectionString, dbOptions =>
        {
            dbOptions.MigrationsAssembly(ReportingPersistence.MigrationsAssembly);
            dbOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, ReportingPersistence.Schema);
        });

        return new ReportingSqlServerDbContext(
            optionsBuilder.Options,
            DesignTimeDbContextDependencies.TenantContext);
    }
}
