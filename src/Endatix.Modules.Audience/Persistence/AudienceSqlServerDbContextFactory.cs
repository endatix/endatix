using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Endatix.Modules.Audience.Persistence;

/// <summary>
/// Design-time factory for SQL Server migrations. Use with
/// <c>--startup-project src/Endatix.WebHost --context AudienceSqlServerDbContext</c> and a SQL Server
/// <c>ConnectionStrings:DefaultConnection</c>.
/// </summary>
/// <remarks>
/// Pins the provider rather than reading <c>DefaultConnection_DbProvider</c>, so the locally configured
/// provider cannot steer which chain a migration lands in.
/// </remarks>
public sealed class AudienceSqlServerDbContextFactory
    : IDesignTimeDbContextFactory<AudienceSqlServerDbContext>
{
    public AudienceSqlServerDbContext CreateDbContext(string[] args)
    {
        var configuration = ModuleDesignTimeConfiguration.Build();
        var connectionString = ModuleDesignTimeConfiguration.GetDefaultConnectionString(configuration);

        var optionsBuilder = new DbContextOptionsBuilder<AudienceSqlServerDbContext>();
        optionsBuilder.UseSqlServer(connectionString, dbOptions =>
        {
            dbOptions.MigrationsAssembly(
                AudiencePersistence.AssemblyName(typeof(AudienceSqlServerDbContext)));
            dbOptions.MigrationsHistoryTable(
                HistoryRepository.DefaultTableName,
                AudiencePersistence.Schema);
        });

        return new AudienceSqlServerDbContext(
            optionsBuilder.Options,
            DesignTimeDbContextDependencies.TenantContext);
    }
}
