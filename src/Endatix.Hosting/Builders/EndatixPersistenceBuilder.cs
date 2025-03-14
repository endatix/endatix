using System.Reflection;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity;
using Endatix.Persistence.PostgreSql.Options;
using Endatix.Persistence.PostgreSql.Setup;
using Endatix.Persistence.SqlServer.Options;
using Endatix.Persistence.SqlServer.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Builder for configuring Endatix persistence.
/// </summary>
public class EndatixPersistenceBuilder
{
    private readonly EndatixBuilder _parentBuilder;
    private readonly ILogger? _logger;

    // Only track default values that will be applied if not specified in configuration
    private bool _autoMigrationsSetting = false;
    private bool _sampleDataSeedingSetting = false;

    // Track whether configurations have been applied
    private bool _configurationsApplied = false;

    /// <summary>
    /// Initializes a new instance of the EndatixPersistenceBuilder class.
    /// </summary>
    /// <param name="parentBuilder">The parent builder.</param>
    internal EndatixPersistenceBuilder(EndatixBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
        _logger = parentBuilder.LoggerFactory?.CreateLogger<EndatixPersistenceBuilder>();
    }

    /// <summary>
    /// Configures persistence with default settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixPersistenceBuilder UseDefaults()
    {
        // For backward compatibility, default to SQL Server
        UseDefaults(DatabaseProvider.SqlServer);

        return this;
    }

    /// <summary>
    /// Configures persistence with default settings using the specified database provider.
    /// </summary>
    /// <param name="databaseProvider">The database provider to use.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixPersistenceBuilder UseDefaults(DatabaseProvider databaseProvider)
    {
        switch (databaseProvider)
        {
            case DatabaseProvider.SqlServer:
                _parentBuilder.Services.AddSqlServerPersistence<AppDbContext>(_parentBuilder.LoggerFactory);
                _parentBuilder.Services.AddSqlServerPersistence<AppIdentityDbContext>(_parentBuilder.LoggerFactory);
                LogSetupInfo("Using default persistence configuration with SQL Server");
                break;
            case DatabaseProvider.PostgreSql:
                _parentBuilder.Services.AddPostgreSqlPersistence<AppDbContext>(_parentBuilder.LoggerFactory);
                _parentBuilder.Services.AddPostgreSqlPersistence<AppIdentityDbContext>(_parentBuilder.LoggerFactory);
                LogSetupInfo("Using default persistence configuration with PostgreSQL");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(databaseProvider), databaseProvider, "Unsupported database provider");
        }

        // Enable auto migrations and data seeding by default
        EnableAutoMigrations();
        EnableSampleDataSeeding();

        // Apply configurations immediately for the default flow
        EnsureConfigurationsApplied();

        return this;
    }

    /// <summary>
    /// Configures PostgreSQL persistence with default settings.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public EndatixPersistenceBuilder UsePostgreSql<TContext>() where TContext : DbContext
    {
        LogSetupInfo($"Configuring PostgreSQL persistence for {typeof(TContext).Name}");

        Endatix.Persistence.PostgreSql.Setup.EndatixPersistenceExtensions.AddPostgreSqlPersistence<TContext>(
            _parentBuilder.Services,
            _parentBuilder.LoggerFactory);

        LogSetupInfo($"PostgreSQL persistence for {typeof(TContext).Name} configured successfully");
        return this;
    }

    /// <summary>
    /// Configures PostgreSQL persistence with custom options.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="configAction">The configuration action for PostgreSQL options.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixPersistenceBuilder UsePostgreSql<TContext>(Action<PostgreSqlOptions> configAction) where TContext : DbContext
    {
        LogSetupInfo($"Configuring PostgreSQL persistence for {typeof(TContext).Name} with custom options");

        Endatix.Persistence.PostgreSql.Setup.EndatixPersistenceExtensions.AddPostgreSqlPersistence<TContext>(
            _parentBuilder.Services,
            configAction,
            _parentBuilder.LoggerFactory);

        LogSetupInfo($"PostgreSQL persistence for {typeof(TContext).Name} configured successfully");
        return this;
    }

    /// <summary>
    /// Configures SQL Server persistence with default settings.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public EndatixPersistenceBuilder UseSqlServer<TContext>() where TContext : DbContext
    {
        LogSetupInfo($"Configuring SQL Server persistence for {typeof(TContext).Name}");

        Endatix.Persistence.SqlServer.Setup.EndatixPersistenceExtensions.AddSqlServerPersistence<TContext>(
            _parentBuilder.Services,
            _parentBuilder.LoggerFactory);

        LogSetupInfo($"SQL Server persistence for {typeof(TContext).Name} configured successfully");
        return this;
    }

    /// <summary>
    /// Configures SQL Server persistence with custom options.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="configAction">The configuration action for SQL Server options.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixPersistenceBuilder UseSqlServer<TContext>(Action<SqlServerOptions> configAction) where TContext : DbContext
    {
        LogSetupInfo($"Configuring SQL Server persistence for {typeof(TContext).Name} with custom options");

        Persistence.SqlServer.Setup.EndatixPersistenceExtensions.AddSqlServerPersistence<TContext>(
            _parentBuilder.Services,
            configAction,
            _parentBuilder.LoggerFactory);

        LogSetupInfo($"SQL Server persistence for {typeof(TContext).Name} configured successfully");
        return this;
    }

    /// <summary>
    /// Enables automatic database migrations at application startup.
    /// </summary>
    /// <param name="applyOnStartup">Whether to apply migrations at startup. Default is true.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixPersistenceBuilder EnableAutoMigrations(bool applyOnStartup = true)
    {
        // Just track the setting - will be applied if not in configuration
        _autoMigrationsSetting = applyOnStartup;

        // Register the service that will check the DataOptions at runtime
        _parentBuilder.Services.AddHostedService<DatabaseMigrationService>();

        return this;
    }

    /// <summary>
    /// Configures entity scanning from specified assemblies.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for entities.</param>
    /// <returns>The persistence builder for chaining.</returns>
    public EndatixPersistenceBuilder ScanAssembliesForEntities(params Assembly[] assemblies)
    {
        // Register assemblies for entity scanning

        return this;
    }

    /// <summary>
    /// Enables sample data seeding, including the initial user.
    /// </summary>
    /// <param name="seedOnStartup">Whether to seed data automatically on startup (default: true).</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixPersistenceBuilder EnableSampleDataSeeding(bool seedOnStartup = true)
    {
        // Just track the setting - will be applied if not in configuration
        _sampleDataSeedingSetting = seedOnStartup;

        // Register the service that will check the DataOptions at runtime
        _parentBuilder.Services.AddHostedService<DataSeedingService>();

        return this;
    }

    /// <summary>
    /// Builds and returns the parent builder.
    /// </summary>
    /// <returns>The parent builder.</returns>
    public EndatixBuilder Build()
    {
        // Apply all tracked configurations before returning the parent builder
        EnsureConfigurationsApplied();

        return _parentBuilder;
    }

    /// <summary>
    /// Ensures that default values are applied to DataOptions if not specified in configuration.
    /// This method guarantees that configuration is only applied once.
    /// </summary>
    /// <remarks>
    /// EndatixPersistenceBuilder is the sole owner of DataOptions configuration.
    /// </remarks>
    private void EnsureConfigurationsApplied()
    {
        // Only apply configurations once
        if (_configurationsApplied)
        {
            return;
        }

        var sectionName = DataOptions.GetSectionName<DataOptions>();
        var section = _parentBuilder.Configuration.GetSection(sectionName);

        // First, register the options with validation
        _parentBuilder.Services.AddOptions<DataOptions>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Then configure with our defaults where not specified in configuration
        _parentBuilder.Services.Configure<DataOptions>(opts =>
        {
            // Check if EnableAutoMigrations is in configuration
            var enableAutoMigrationsConfigValue = section.GetSection(nameof(DataOptions.EnableAutoMigrations));
            if (!enableAutoMigrationsConfigValue.Exists())
            {
                // Not in config - apply our tracked default
                _logger?.LogDebug("Setting EnableAutoMigrations={Setting} from code default", _autoMigrationsSetting);
                opts.EnableAutoMigrations = _autoMigrationsSetting;
            }
            else
            {
                _logger?.LogDebug("Using EnableAutoMigrations={Setting} value from configuration", enableAutoMigrationsConfigValue.Value);
            }

            // Check if SeedSampleData is in configuration
            var seedSampleDataConfigValue = section.GetSection(nameof(DataOptions.SeedSampleData));
            if (!seedSampleDataConfigValue.Exists())
            {
                // Not in config - apply our tracked default
                _logger?.LogDebug("Setting SeedSampleData={Setting} from code default", _sampleDataSeedingSetting);
                opts.SeedSampleData = _sampleDataSeedingSetting;
            }
            else
            {
                _logger?.LogDebug("Using SeedSampleData={Setting} value from configuration", seedSampleDataConfigValue.Value);
            }
        });

        _configurationsApplied = true;
    }

    private void LogSetupInfo(string message)
    {
        _logger?.LogInformation("[Persistence Setup] {Message}", message);
    }
}

/// <summary>
/// Supported database providers for the Endatix persistence layer.
/// </summary>
public enum DatabaseProvider
{
    /// <summary>
    /// Microsoft SQL Server
    /// </summary>
    SqlServer,

    /// <summary>
    /// PostgreSQL
    /// </summary>
    PostgreSql
}