using System.Globalization;
using System.Reflection;
using System.Threading;
using Endatix.Hosting.Builders.Logging;
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
    private int? _snowflakeWorkerIdSetting;
    private int _snowflakeWorkerIdWarningLogged;

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
        // Register migration/seeding hosted services before persistence. Persistence registers
        // BackgroundService relays (outbox) that return from StartAsync immediately; they must
        // run after DatabaseMigrationService and DataSeedingService have completed StartAsync.
        EnableAutoMigrations();
        EnableSampleDataSeeding();

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
    /// <remarks>
    /// Register this before persistence builders that add database-backed background relays
    /// (for example the outbox relay), so database migrations complete before those loops begin.
    /// </remarks>
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
    /// Sets the Snowflake worker id used when <c>Endatix:Data:SnowflakeWorkerId</c> is not set.
    /// When neither is set, the worker id keeps any value from <c>Infrastructure.Data.Configure</c>, else 0.
    /// </summary>
    /// <param name="workerId">The worker id, from 0 to 1023.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixPersistenceBuilder UseSnowflakeWorkerId(int workerId)
    {
        _snowflakeWorkerIdSetting = workerId;
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

        // Then configure with our defaults where not specified in configuration.
        // The callbacks read the tracked fields when options are resolved, so a later builder call still applies.
        _parentBuilder.Services.Configure<DataOptions>(opts =>
        {
            if (MissingFromConfiguration(section, nameof(DataOptions.EnableAutoMigrations), _autoMigrationsSetting.ToString()))
            {
                opts.EnableAutoMigrations = _autoMigrationsSetting;
            }

            if (MissingFromConfiguration(section, nameof(DataOptions.SeedSampleData), _sampleDataSeedingSetting.ToString()))
            {
                opts.SeedSampleData = _sampleDataSeedingSetting;
            }
            ApplySnowflakeWorkerId(section, opts);
        });

        // Runs after every ordinary Configure<DataOptions> callback, so a host callback registered later
        // (Infrastructure.Data.Configure) cannot override the worker id the configuration sets.
        _parentBuilder.Services.PostConfigure<DataOptions>(opts => ReapplyConfiguredWorkerId(section, opts));

        _configurationsApplied = true;
    }

    private static void ReapplyConfiguredWorkerId(IConfigurationSection dataSection, DataOptions opts)
    {
        var configuredText = dataSection.GetSection(nameof(DataOptions.SnowflakeWorkerId)).Value;
        if (configuredText is not null &&
            int.TryParse(configuredText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var configuredValue))
        {
            opts.SnowflakeWorkerId = configuredValue;
        }
    }

    private bool MissingFromConfiguration(IConfigurationSection dataSection, string settingName, string trackedValue)
    {
        var configured = dataSection.GetSection(settingName);
        if (!configured.Exists())
        {
            _logger?.LogDataOptionFromDefault(settingName, trackedValue);
            return true;
        }

        _logger?.LogDataOptionFromConfiguration(settingName, configured.Value ?? string.Empty);
        return false;
    }

    private void ApplySnowflakeWorkerId(IConfigurationSection dataSection, DataOptions opts)
    {
        const string settingName = nameof(DataOptions.SnowflakeWorkerId);
        var configuredText = dataSection.GetSection(settingName).Value;
        if (configuredText is null)
        {
            if (_snowflakeWorkerIdSetting is int workerId)
            {
                opts.SnowflakeWorkerId = workerId;
                LogWorkerIdFromDefault(settingName, workerId);
            }

            return;
        }

        _logger?.LogDataOptionFromConfiguration(settingName, configuredText);
        WarnWhenSnowflakeConfigurationWins(configuredText);
    }

    private void LogWorkerIdFromDefault(string settingName, int workerId)
    {
        if (_logger is not ILogger logger || !logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        logger.LogDataOptionFromDefault(settingName, workerId.ToString(CultureInfo.InvariantCulture));
    }

    private void WarnWhenSnowflakeConfigurationWins(string configuredText)
    {
        if (_snowflakeWorkerIdSetting is not int builderValue ||
            !int.TryParse(configuredText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var configuredValue) ||
            builderValue == configuredValue ||
            Interlocked.Exchange(ref _snowflakeWorkerIdWarningLogged, 1) != 0)
        {
            return;
        }

        _logger?.LogSnowflakeWorkerIdConfigurationWins(configuredText, builderValue);
    }

    private void LogSetupInfo(string message)
    {
        _logger?.LogPersistenceSetup(message);
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
