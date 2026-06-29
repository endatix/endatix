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

        ModuleDesignTimeConfiguration.GetDefaultConnectionString(configuration);

        services.AddDbContext<TContext>(options =>
            options.ConfigureModuleDbContext(configuration, moduleOptions));

        return services;
    }

    /// <summary>
    /// Configures a module DbContext for design-time tooling or explicit options building.
    /// </summary>
    public static void ConfigureModuleDbContext(
        this DbContextOptionsBuilder optionsBuilder,
        IConfiguration configuration,
        ModuleDbContextOptions moduleOptions)
    {
        Guard.Against.Null(optionsBuilder);
        Guard.Against.Null(configuration);
        Guard.Against.Null(moduleOptions);

        var connectionString = ModuleDesignTimeConfiguration.GetDefaultConnectionString(configuration);
        var usePostgreSql = DatabaseProviderResolver.IsPostgreSql(configuration);
        var migrationsNamespace = usePostgreSql
            ? moduleOptions.PostgreSqlMigrationsNamespace
            : moduleOptions.SqlServerMigrationsNamespace;
        var useProviderNamespaceFiltering = !string.Equals(
            moduleOptions.PostgreSqlMigrationsNamespace,
            moduleOptions.SqlServerMigrationsNamespace,
            StringComparison.Ordinal);

        if (usePostgreSql)
        {
            optionsBuilder.UseNpgsql(connectionString, dbOptions =>
            {
                dbOptions.MigrationsAssembly(moduleOptions.MigrationsAssembly);
                dbOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, moduleOptions.Schema);
            });
        }
        else
        {
            optionsBuilder.UseSqlServer(connectionString, dbOptions =>
            {
                dbOptions.MigrationsAssembly(moduleOptions.MigrationsAssembly);
                dbOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, moduleOptions.Schema);
            });
        }

        if (useProviderNamespaceFiltering)
        {
            ConfigureProviderScopedMigrations(optionsBuilder, migrationsNamespace);
        }
    }

    /// <summary>
    /// Configures a module DbContext using the same options callback as runtime registration.
    /// </summary>
    public static void ConfigureModuleDbContext<TContext>(
        this DbContextOptionsBuilder optionsBuilder,
        IConfiguration configuration,
        Action<ModuleDbContextOptions> configure)
        where TContext : DbContext
    {
        Guard.Against.Null(configure);

        var moduleOptions = new ModuleDbContextOptions();
        configure(moduleOptions);

        Guard.Against.NullOrEmpty(moduleOptions.MigrationsAssembly);
        Guard.Against.NullOrEmpty(moduleOptions.PostgreSqlMigrationsNamespace);
        Guard.Against.NullOrEmpty(moduleOptions.SqlServerMigrationsNamespace);

        optionsBuilder.ConfigureModuleDbContext(configuration, moduleOptions);
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
