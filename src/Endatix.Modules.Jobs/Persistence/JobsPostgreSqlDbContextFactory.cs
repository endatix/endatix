using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Endatix.Modules.Jobs.Persistence;

/// <summary>
/// Design-time factory for PostgreSQL migrations. Use with
/// <c>--startup-project src/Endatix.WebHost --context JobsPostgreSqlDbContext</c>.
/// </summary>
/// <remarks>
/// Pins the provider rather than reading <c>DefaultConnection_DbProvider</c>, so generating for one
/// provider can never be steered by whichever connection happens to be configured locally. There is
/// no hardcoded connection-string fallback: a missing <c>ConnectionStrings:DefaultConnection</c>
/// fails loudly rather than silently generating against a phantom database.
/// </remarks>
public sealed class JobsPostgreSqlDbContextFactory : IDesignTimeDbContextFactory<JobsPostgreSqlDbContext>
{
    public JobsPostgreSqlDbContext CreateDbContext(string[] args)
    {
        var configuration = ModuleDesignTimeConfiguration.Build();
        var connectionString = ModuleDesignTimeConfiguration.GetDefaultConnectionString(configuration);

        var optionsBuilder = new DbContextOptionsBuilder<JobsPostgreSqlDbContext>();
        optionsBuilder.UseNpgsql(connectionString, dbOptions =>
        {
            dbOptions.MigrationsAssembly(typeof(JobsPostgreSqlDbContext).Assembly.GetName().Name!);
            dbOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, JobsPersistence.Schema);
        });

        return new JobsPostgreSqlDbContext(
            optionsBuilder.Options,
            DesignTimeDbContextDependencies.TenantContext,
            new EfCoreValueGeneratorFactory(DesignTimeDbContextDependencies.IdGenerator));
    }
}
