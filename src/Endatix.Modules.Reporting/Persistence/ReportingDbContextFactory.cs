using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Endatix.Infrastructure.Data;

namespace Endatix.Modules.Reporting.Persistence;

/// <summary>
/// Design-time factory for EF Core migrations. Use with <c>--startup-project src/Endatix.WebHost</c>.
/// </summary>
public sealed class ReportingDbContextFactory : IDesignTimeDbContextFactory<ReportingDbContext>
{
    public ReportingDbContext CreateDbContext(string[] args)
    {
        var configuration = ModuleDesignTimeConfiguration.Build();
        var optionsBuilder = new DbContextOptionsBuilder<ReportingDbContext>();
        optionsBuilder.ConfigureModuleDbContext(configuration, ReportingPersistence.ConfigureDbContextOptions);

        return new ReportingDbContext(
            optionsBuilder.Options,
            DesignTimeDbContextDependencies.IdGenerator,
            DesignTimeDbContextDependencies.TenantContext);
    }
}
