using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Endatix.Modules.Audience.Persistence;

/// <summary>
/// Design-time factory for PostgreSQL migrations. Use with
/// <c>--startup-project src/Endatix.WebHost --context AudiencePostgreSqlDbContext</c>.
/// </summary>
public sealed class AudiencePostgreSqlDbContextFactory
    : IDesignTimeDbContextFactory<AudiencePostgreSqlDbContext>
{
    public AudiencePostgreSqlDbContext CreateDbContext(string[] args)
    {
        var configuration = ModuleDesignTimeConfiguration.Build();
        var connectionString = ModuleDesignTimeConfiguration.GetDefaultConnectionString(configuration);

        var optionsBuilder = new DbContextOptionsBuilder<AudiencePostgreSqlDbContext>();
        optionsBuilder.UseNpgsql(connectionString, dbOptions =>
        {
            dbOptions.MigrationsAssembly(
                AudiencePersistence.AssemblyName(typeof(AudiencePostgreSqlDbContext)));
            dbOptions.MigrationsHistoryTable(
                HistoryRepository.DefaultTableName,
                AudiencePersistence.Schema);
        });

        return new AudiencePostgreSqlDbContext(
            optionsBuilder.Options,
            DesignTimeDbContextDependencies.TenantContext);
    }
}
