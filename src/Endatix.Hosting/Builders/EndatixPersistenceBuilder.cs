using System;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Endatix.Infrastructure.Data;
using Endatix.Persistence.SqlServer.Setup;
using Endatix.Persistence.PostgreSql.Setup;
using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Identity;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Builder for configuring Endatix persistence.
/// </summary>
public class EndatixPersistenceBuilder
{
    private readonly EndatixBuilder _parentBuilder;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the EndatixPersistenceBuilder class.
    /// </summary>
    /// <param name="parentBuilder">The parent builder.</param>
    internal EndatixPersistenceBuilder(EndatixBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
        _logger = parentBuilder.LoggerFactory?.CreateLogger("Endatix.Setup");
    }

    /// <summary>
    /// Configures persistence with default settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixPersistenceBuilder UseDefaults()
    {
        // For backward compatibility, default to SQL Server
        return UseDefaults(DatabaseProvider.SqlServer);
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

        // Note: ID generator is now registered centrally in EndatixServiceCollectionExtensions.AddEndatixCorePersistenceServices
        
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

        // This dynamically looks up the PostgreSQL extension methods
        var method = typeof(Endatix.Persistence.PostgreSql.Setup.EndatixPersistenceExtensions)
            .GetMethod("AddPostgreSqlPersistence", new[] { typeof(IServiceCollection), typeof(ILoggerFactory) });

        if (method != null)
        {
            method.MakeGenericMethod(typeof(TContext))
                .Invoke(null, new object[] { _parentBuilder.Services, _parentBuilder.LoggerFactory });

            LogSetupInfo($"PostgreSQL persistence for {typeof(TContext).Name} configured successfully");
        }
        else
        {
            throw new InvalidOperationException("PostgreSQL persistence extension methods not found. Make sure Endatix.Persistence.PostgreSql is referenced.");
        }

        return this;
    }

    /// <summary>
    /// Configures PostgreSQL persistence with custom options.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="configAction">The configuration action for PostgreSQL options.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixPersistenceBuilder UsePostgreSql<TContext>(Action<object> configAction) where TContext : DbContext
    {
        LogSetupInfo($"Configuring PostgreSQL persistence for {typeof(TContext).Name} with custom options");

        // Find PostgreSQL option type
        var optionsType = Type.GetType("Endatix.Persistence.PostgreSql.Options.PostgreSqlOptions, Endatix.Persistence.PostgreSql");
        if (optionsType == null)
        {
            throw new InvalidOperationException("PostgreSQL options type not found. Make sure Endatix.Persistence.PostgreSql is referenced.");
        }

        // This dynamically looks up the PostgreSQL extension methods with options
        var method = typeof(Endatix.Persistence.PostgreSql.Setup.EndatixPersistenceExtensions)
            .GetMethod("AddPostgreSqlPersistence", new[] { typeof(IServiceCollection), optionsType.MakeByRefType(), typeof(ILoggerFactory) });

        if (method != null)
        {
            // Create adapter that will convert the generic Action<object> to Action<PostgreSqlOptions>
            var adapterType = typeof(ActionAdapter<>).MakeGenericType(optionsType);
            var adapter = Activator.CreateInstance(adapterType, configAction);
            var actionMethod = adapterType.GetMethod("GetTypedAction");
            var typedAction = actionMethod?.Invoke(adapter, Array.Empty<object>());

            method.MakeGenericMethod(typeof(TContext))
                .Invoke(null, new object[] { _parentBuilder.Services, typedAction, _parentBuilder.LoggerFactory });

            LogSetupInfo($"PostgreSQL persistence for {typeof(TContext).Name} configured successfully");
        }
        else
        {
            throw new InvalidOperationException("PostgreSQL persistence extension methods with options not found. Make sure Endatix.Persistence.PostgreSql is referenced.");
        }

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

        // This dynamically looks up the SQL Server extension methods
        var method = typeof(Endatix.Persistence.SqlServer.Setup.EndatixPersistenceExtensions)
            .GetMethod("AddSqlServerPersistence", new[] { typeof(IServiceCollection), typeof(ILoggerFactory) });

        if (method != null)
        {
            method.MakeGenericMethod(typeof(TContext))
                .Invoke(null, new object[] { _parentBuilder.Services, _parentBuilder.LoggerFactory });

            LogSetupInfo($"SQL Server persistence for {typeof(TContext).Name} configured successfully");
        }
        else
        {
            throw new InvalidOperationException("SQL Server persistence extension methods not found. Make sure Endatix.Persistence.SqlServer is referenced.");
        }

        return this;
    }

    /// <summary>
    /// Configures SQL Server persistence with custom options.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="configAction">The configuration action for SQL Server options.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixPersistenceBuilder UseSqlServer<TContext>(Action<object> configAction) where TContext : DbContext
    {
        LogSetupInfo($"Configuring SQL Server persistence for {typeof(TContext).Name} with custom options");

        // Find SQL Server option type
        var optionsType = Type.GetType("Endatix.Persistence.SqlServer.Options.SqlServerOptions, Endatix.Persistence.SqlServer");
        if (optionsType == null)
        {
            throw new InvalidOperationException("SQL Server options type not found. Make sure Endatix.Persistence.SqlServer is referenced.");
        }

        // This dynamically looks up the SQL Server extension methods with options
        var method = typeof(Endatix.Persistence.SqlServer.Setup.EndatixPersistenceExtensions)
            .GetMethod("AddSqlServerPersistence", new[] { typeof(IServiceCollection), optionsType.MakeByRefType(), typeof(ILoggerFactory) });

        if (method != null)
        {
            // Create adapter that will convert the generic Action<object> to Action<SqlServerOptions>
            var adapterType = typeof(ActionAdapter<>).MakeGenericType(optionsType);
            var adapter = Activator.CreateInstance(adapterType, configAction);
            var actionMethod = adapterType.GetMethod("GetTypedAction");
            var typedAction = actionMethod?.Invoke(adapter, Array.Empty<object>());

            method.MakeGenericMethod(typeof(TContext))
                .Invoke(null, new object[] { _parentBuilder.Services, typedAction, _parentBuilder.LoggerFactory });

            LogSetupInfo($"SQL Server persistence for {typeof(TContext).Name} configured successfully");
        }
        else
        {
            throw new InvalidOperationException("SQL Server persistence extension methods with options not found. Make sure Endatix.Persistence.SqlServer is referenced.");
        }

        return this;
    }

    /// <summary>
    /// Enables automatic database migrations.
    /// </summary>
    /// <returns>The persistence builder for chaining.</returns>
    public EndatixPersistenceBuilder EnableAutoMigrations()
    {
        // Configure automatic migrations

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
    /// Returns to the parent builder.
    /// </summary>
    /// <returns>The parent builder.</returns>
    public EndatixBuilder Parent() => _parentBuilder;

    private void LogSetupInfo(string message)
    {
        _logger?.LogInformation("[Persistence Setup] {Message}", message);
    }

    /// <summary>
    /// Adapter to convert generic Action to typed Action.
    /// </summary>
    /// <typeparam name="T">The options type.</typeparam>
    private class ActionAdapter<T>
    {
        private readonly Action<object> _genericAction;

        /// <summary>
        /// Initializes a new instance of the ActionAdapter class.
        /// </summary>
        /// <param name="genericAction">The generic action.</param>
        public ActionAdapter(Action<object> genericAction)
        {
            _genericAction = genericAction;
        }

        /// <summary>
        /// Gets the typed action.
        /// </summary>
        /// <returns>A typed action.</returns>
        public Action<T> GetTypedAction()
        {
            return options => _genericAction(options);
        }
    }
}

/// <summary>
/// Options for SQL Server configuration.
/// </summary>
public class SqlServerOptions
{
    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Configures the connection string.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The options for chaining.</returns>
    public SqlServerOptions WithConnectionString(string connectionString)
    {
        ConnectionString = connectionString;
        return this;
    }

    /// <summary>
    /// Configures default settings.
    /// </summary>
    /// <returns>The options for chaining.</returns>
    public SqlServerOptions WithDefaultConfiguration()
    {
        // Configure default settings
        return this;
    }

    /// <summary>
    /// Configures a custom table prefix.
    /// </summary>
    /// <param name="prefix">The table prefix.</param>
    /// <returns>The options for chaining.</returns>
    public SqlServerOptions WithCustomTablePrefix(string prefix = "end_")
    {
        // Configure table prefix
        return this;
    }

    /// <summary>
    /// Configures Snowflake IDs.
    /// </summary>
    /// <param name="workerId">The worker ID.</param>
    /// <returns>The options for chaining.</returns>
    public SqlServerOptions WithSnowflakeIds(int workerId)
    {
        // Store the worker ID for later use during service registration
        WorkerId = workerId;
        UseSnowflakeIds = true;
        return this;
    }

    /// <summary>
    /// Gets or sets the worker ID for Snowflake ID generation.
    /// </summary>
    internal int WorkerId { get; private set; }

    /// <summary>
    /// Gets or sets whether to use Snowflake IDs.
    /// </summary>
    internal bool UseSnowflakeIds { get; private set; }

    /// <summary>
    /// Configures sample data.
    /// </summary>
    /// <returns>The options for chaining.</returns>
    public SqlServerOptions WithSampleData()
    {
        // Configure sample data
        return this;
    }
}

/// <summary>
/// Options for PostgreSQL configuration.
/// </summary>
public class PostgresOptions
{
    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Configures the connection string.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The options for chaining.</returns>
    public PostgresOptions WithConnectionString(string connectionString)
    {
        ConnectionString = connectionString;
        return this;
    }

    /// <summary>
    /// Configures default settings.
    /// </summary>
    /// <returns>The options for chaining.</returns>
    public PostgresOptions WithDefaultConfiguration()
    {
        // Configure default settings
        return this;
    }

    /// <summary>
    /// Configures a custom table prefix.
    /// </summary>
    /// <param name="prefix">The table prefix.</param>
    /// <returns>The options for chaining.</returns>
    public PostgresOptions WithCustomTablePrefix(string prefix = "end_")
    {
        // Configure table prefix
        return this;
    }

    /// <summary>
    /// Configures Snowflake IDs.
    /// </summary>
    /// <param name="workerId">The worker ID.</param>
    /// <returns>The options for chaining.</returns>
    public PostgresOptions WithSnowflakeIds(int workerId)
    {
        // Configure Snowflake IDs
        return this;
    }

    /// <summary>
    /// Configures sample data.
    /// </summary>
    /// <returns>The options for chaining.</returns>
    public PostgresOptions WithSampleData()
    {
        // Configure sample data
        return this;
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