using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Persistence.PostgreSql.Querying;
using Endatix.Persistence.SqlServer.Querying;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

/// <summary>
/// Builds <see cref="AppDbContext" /> for EF model/metadata inspection without opening a connection.
/// </summary>
internal static class AppDbContextModelInspectionFactory
{
    private const string PostgresMigrationsAssembly = "Endatix.Persistence.PostgreSql";
    private const string PostgresAppMigrationsNamespace = "Endatix.Persistence.PostgreSql.Migrations.AppEntities";
    private const string SqlServerMigrationsAssembly = "Endatix.Persistence.SqlServer";
    private const string SqlServerAppMigrationsNamespace = "Endatix.Persistence.SqlServer.Migrations.AppEntities";

    internal static AppDbContext CreatePostgreSqlAppDbContext(
        IIdGenerator<long>? idGenerator = null,
        ITenantContext? tenantContext = null)
    {
        var resolvedIdGenerator = idGenerator ?? Substitute.For<IIdGenerator<long>>();
        var resolvedTenantContext = tenantContext ?? Substitute.For<ITenantContext>();

        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(
            "Host=127.0.0.1;Database=__ef_model_inspection_not_connected__;Username=postgres;Password=postgres",
            npgsql =>
            {
                npgsql.MigrationsAssembly(PostgresMigrationsAssembly);
                npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName);
            });
        optionsBuilder.ReplaceService<IModelCustomizer, NpgsqlEndatixModelCustomizer>();
        ModuleDbContextExtensions.ConfigureProviderScopedMigrations(
            optionsBuilder,
            PostgresAppMigrationsNamespace);

        return new AppDbContext(
            optionsBuilder.Options,
            resolvedIdGenerator,
            resolvedTenantContext,
            new EfCoreValueGeneratorFactory(resolvedIdGenerator),
            new OutboxIntegrationEventDispatcher());
    }

    internal static AppDbContext CreateSqlServerAppDbContext(
        IIdGenerator<long>? idGenerator = null,
        ITenantContext? tenantContext = null)
    {
        var resolvedIdGenerator = idGenerator ?? Substitute.For<IIdGenerator<long>>();
        var resolvedTenantContext = tenantContext ?? Substitute.For<ITenantContext>();

        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=__ef_model_inspection_not_connected__;Trusted_Connection=True",
            sqlServer =>
            {
                sqlServer.MigrationsAssembly(SqlServerMigrationsAssembly);
                sqlServer.MigrationsHistoryTable(HistoryRepository.DefaultTableName);
            });
        optionsBuilder.ReplaceService<IModelCustomizer, SqlServerEndatixModelCustomizer>();
        ModuleDbContextExtensions.ConfigureProviderScopedMigrations(
            optionsBuilder,
            SqlServerAppMigrationsNamespace);

        return new AppDbContext(
            optionsBuilder.Options,
            resolvedIdGenerator,
            resolvedTenantContext,
            new EfCoreValueGeneratorFactory(resolvedIdGenerator),
            new OutboxIntegrationEventDispatcher());
    }
}
