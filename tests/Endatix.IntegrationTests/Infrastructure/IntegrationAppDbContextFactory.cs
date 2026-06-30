using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Endatix.IntegrationTests;

/// <summary>
/// Builds <see cref="AppDbContext" /> for integration tests against a real PostgreSQL Testcontainers database.
/// </summary>
internal static class IntegrationAppDbContextFactory
{
    private const string PostgresMigrationsAssembly = "Endatix.Persistence.PostgreSql";
    private const string AppMigrationsNamespace = "Endatix.Persistence.PostgreSql.Migrations.AppEntities";

    internal static DbContextOptionsBuilder<AppDbContext> ConfigurePostgreSqlOptions(
        DbContextOptionsBuilder<AppDbContext> optionsBuilder,
        string connectionString)
    {
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsAssembly(PostgresMigrationsAssembly);
            npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName);
        });
        ModuleDbContextExtensions.ConfigureProviderScopedMigrations(optionsBuilder, AppMigrationsNamespace);
        return optionsBuilder;
    }
}
