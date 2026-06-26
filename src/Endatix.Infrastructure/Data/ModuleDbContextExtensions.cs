using Ardalis.GuardClauses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Registers module-owned DbContext instances with provider-scoped migrations in a shared assembly.
/// </summary>
public static class ModuleDbContextExtensions
{
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<ModuleDbContextOptions> configure)
        where TContext : DbContext
    {
        Guard.Against.Null(services);
        Guard.Against.Null(configuration);
        Guard.Against.Null(configure);

        var moduleOptions = new ModuleDbContextOptions();
        configure(moduleOptions);

        Guard.Against.NullOrEmpty(moduleOptions.MigrationsAssembly);
        Guard.Against.NullOrEmpty(moduleOptions.PostgreSqlMigrationsNamespace);
        Guard.Against.NullOrEmpty(moduleOptions.SqlServerMigrationsNamespace);

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var usePostgreSql = DatabaseProviderResolver.IsPostgreSql(configuration);
        var migrationsNamespace = usePostgreSql
            ? moduleOptions.PostgreSqlMigrationsNamespace
            : moduleOptions.SqlServerMigrationsNamespace;

        services.AddDbContext<TContext>(options =>
        {
            if (usePostgreSql)
            {
                options.UseNpgsql(connectionString, dbOptions =>
                {
                    dbOptions.MigrationsAssembly(moduleOptions.MigrationsAssembly);
                    dbOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, moduleOptions.Schema);
                });
            }
            else
            {
                options.UseSqlServer(connectionString, dbOptions =>
                {
                    dbOptions.MigrationsAssembly(moduleOptions.MigrationsAssembly);
                    dbOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, moduleOptions.Schema);
                });
            }

            ConfigureProviderScopedMigrations(options, migrationsNamespace);
        });

        return services;
    }

    /// <summary>
    /// Configures provider-scoped migration discovery for design-time tooling.
    /// </summary>
    public static void ConfigureProviderScopedMigrations(
        DbContextOptionsBuilder optionsBuilder,
        string migrationsNamespace)
    {
        var extension = optionsBuilder.Options.FindExtension<ProviderMigrationsNamespaceExtension>()
            ?? new ProviderMigrationsNamespaceExtension();

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder)
            .AddOrUpdateExtension(extension.WithNamespace(migrationsNamespace));

        optionsBuilder.ReplaceService<IMigrationsAssembly, NamespaceFilteringMigrationsAssembly>();
    }
}
