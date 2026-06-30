using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

/// <summary>
/// Builds <see cref="AppDbContext" /> for EF model/metadata inspection without opening a connection.
/// </summary>
internal static class AppDbContextModelInspectionFactory
{
    private const string PostgresMigrationsAssembly = "Endatix.Persistence.PostgreSql";
    private const string AppMigrationsNamespace = "Endatix.Persistence.PostgreSql.Migrations.AppEntities";

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
        ModuleDbContextExtensions.ConfigureProviderScopedMigrations(optionsBuilder, AppMigrationsNamespace);

        return new AppDbContext(
            optionsBuilder.Options,
            resolvedIdGenerator,
            resolvedTenantContext,
            new EfCoreValueGeneratorFactory(resolvedIdGenerator),
            new OutboxIntegrationEventDispatcher());
    }
}
