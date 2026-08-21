using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Endatix.Modules.Jobs.Persistence;

/// <summary>
/// Design-time factory for SQL Server migrations. Use with
/// <c>--startup-project src/Endatix.WebHost --context JobsSqlServerDbContext</c>.
/// </summary>
/// <remarks>
/// Pins the provider rather than reading <c>DefaultConnection_DbProvider</c>, so generating for one
/// provider can never be steered by whichever connection happens to be configured locally. There is
/// no hardcoded connection-string fallback: a missing <c>ConnectionStrings:DefaultConnection</c>
/// fails loudly rather than silently generating against a phantom database.
/// </remarks>
public sealed class JobsSqlServerDbContextFactory : IDesignTimeDbContextFactory<JobsSqlServerDbContext>
{
    public JobsSqlServerDbContext CreateDbContext(string[] args)
    {
        var configuration = ModuleDesignTimeConfiguration.Build();
        var connectionString = ModuleDesignTimeConfiguration.GetDefaultConnectionString(configuration);

        var optionsBuilder = new DbContextOptionsBuilder<JobsSqlServerDbContext>();
        optionsBuilder.UseSqlServer(connectionString, dbOptions =>
        {
            dbOptions.MigrationsAssembly(typeof(JobsSqlServerDbContext).Assembly.GetName().Name!);
            dbOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, JobsPersistence.Schema);
        });

        return new JobsSqlServerDbContext(
            optionsBuilder.Options,
            DesignTimeDbContextDependencies.TenantContext,
            new EfCoreValueGeneratorFactory(DesignTimeDbContextDependencies.IdGenerator));
    }
}
