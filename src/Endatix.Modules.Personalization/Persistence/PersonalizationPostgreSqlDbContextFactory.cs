using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Endatix.Modules.Personalization.Persistence;

/// <summary>
/// Design-time factory for PostgreSQL migrations. Use with
/// <c>--startup-project src/Endatix.WebHost --context PersonalizationPostgreSqlDbContext</c>.
/// </summary>
public sealed class PersonalizationPostgreSqlDbContextFactory
    : IDesignTimeDbContextFactory<PersonalizationPostgreSqlDbContext>
{
    public PersonalizationPostgreSqlDbContext CreateDbContext(string[] args)
    {
        var configuration = ModuleDesignTimeConfiguration.Build();
        var connectionString = ModuleDesignTimeConfiguration.GetDefaultConnectionString(configuration);

        var optionsBuilder = new DbContextOptionsBuilder<PersonalizationPostgreSqlDbContext>();
        optionsBuilder.UseNpgsql(connectionString, dbOptions =>
        {
            dbOptions.MigrationsAssembly(typeof(PersonalizationPostgreSqlDbContext).Assembly.GetName().Name!);
            dbOptions.MigrationsHistoryTable(
                HistoryRepository.DefaultTableName,
                PersonalizationPersistence.Schema);
        });

        return new PersonalizationPostgreSqlDbContext(
            optionsBuilder.Options,
            DesignTimeDbContextDependencies.TenantContext);
    }
}
