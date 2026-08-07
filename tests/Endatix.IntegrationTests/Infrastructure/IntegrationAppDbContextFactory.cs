using Endatix.Infrastructure.Data;
using Endatix.Persistence.PostgreSql.Querying;
using Endatix.Persistence.SqlServer.Querying;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Endatix.IntegrationTests;

/// <summary>
/// Builds <see cref="AppDbContext" /> for integration tests against a real Testcontainers database.
/// </summary>
internal static class IntegrationAppDbContextFactory
{
    private const string PostgresMigrationsAssembly = "Endatix.Persistence.PostgreSql";
    private const string SqlServerMigrationsAssembly = "Endatix.Persistence.SqlServer";
    private const string PostgresAppMigrationsNamespace = "Endatix.Persistence.PostgreSql.Migrations.AppEntities";
    private const string SqlServerAppMigrationsNamespace = "Endatix.Persistence.SqlServer.Migrations.AppEntities";

    internal static DbContextOptionsBuilder<AppDbContext> ConfigurePostgreSqlOptions(
        DbContextOptionsBuilder<AppDbContext> optionsBuilder,
        string connectionString)
    {
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsAssembly(PostgresMigrationsAssembly);
            npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName);
        });
        optionsBuilder.ReplaceService<IModelCustomizer, NpgsqlEndatixModelCustomizer>();
        ModuleDbContextExtensions.ConfigureProviderScopedMigrations(optionsBuilder, PostgresAppMigrationsNamespace);
        return optionsBuilder;
    }

    internal static DbContextOptionsBuilder<AppDbContext> ConfigureSqlServerOptions(
        DbContextOptionsBuilder<AppDbContext> optionsBuilder,
        string connectionString)
    {
        optionsBuilder.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(SqlServerMigrationsAssembly);
            sql.MigrationsHistoryTable(HistoryRepository.DefaultTableName);
            sql.UseCompatibilityLevel(170);
        });
        optionsBuilder.ReplaceService<IModelCustomizer, SqlServerEndatixModelCustomizer>();
        ModuleDbContextExtensions.ConfigureProviderScopedMigrations(optionsBuilder, SqlServerAppMigrationsNamespace);
        return optionsBuilder;
    }

    internal static DbContextOptionsBuilder<AppDbContext> ConfigureOptions(
        DbContextOptionsBuilder<AppDbContext> optionsBuilder,
        string connectionString,
        TestDatabaseProvider provider) =>
        provider == TestDatabaseProvider.SqlServer
            ? ConfigureSqlServerOptions(optionsBuilder, connectionString)
            : ConfigurePostgreSqlOptions(optionsBuilder, connectionString);
}
