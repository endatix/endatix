using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Endatix.Hosting.Options;
using Endatix.Hosting.Logging;
using Endatix.Infrastructure.Builders;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Endatix.Infrastructure.Identity;
using Microsoft.Extensions.Hosting;
using Endatix.Framework.Hosting;
using Endatix.Framework.Setup;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Main builder for configuring Endatix services.
/// </summary>
public class EndatixBuilder : IBuilderParent
{
    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    public IConfiguration Configuration { get; }

    /// <summary>
    /// Gets the application environment.
    /// </summary>
    public IAppEnvironment? AppEnvironment { get; }

    /// <summary>
    /// Gets the API builder.
    /// </summary>
    public EndatixApiBuilder Api { get; }

    /// <summary>
    /// Gets the persistence builder.
    /// </summary>
    public EndatixPersistenceBuilder Persistence { get; }

    /// <summary>
    /// Gets the security builder.
    /// </summary>
    public EndatixSecurityBuilder Security { get; }

    /// <summary>
    /// Gets the messaging builder.
    /// </summary>
    public EndatixMessagingBuilder Messaging { get; }

    /// <summary>
    /// Gets the logging builder.
    /// </summary>
    public EndatixLoggingBuilder Logging { get; }

    /// <summary>
    /// Gets a logger factory that can create loggers for specific categories.
    /// </summary>
    public ILoggerFactory LoggerFactory
    {
        get
        {
            if (Logging.LoggerFactory == null)
            {
                throw new InvalidOperationException("Logger factory not initialized. Ensure logging is configured before using it.");
            }
            return Logging.LoggerFactory;
        }
    }

    private EndatixSetupLogger? _setupLogger;

    /// <summary>
    /// Gets the setup logger for logging during configuration.
    /// </summary>
    internal EndatixSetupLogger SetupLogger
    {
        get
        {
            if (_setupLogger == null)
            {
                var logger = LoggerFactory.CreateLogger("Endatix.Setup");
                _setupLogger = new EndatixSetupLogger(logger);
            }
            return _setupLogger;
        }
    }

    /// <summary>
    /// Gets the infrastructure builder.
    /// </summary>
    public InfrastructureBuilder Infrastructure { get; }

    /// <summary>
    /// Initializes a new instance of the EndatixBuilder class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    internal EndatixBuilder(
        IServiceCollection services,
        IConfiguration configuration)
    {
        Services = services;
        Configuration = configuration;

        // Try to get IAppEnvironment from DI
        var serviceProvider = services.BuildServiceProvider();
        AppEnvironment = serviceProvider.GetService<IAppEnvironment>();

        // Initialize logging builder first to ensure logger factory is available
        Logging = new EndatixLoggingBuilder(this);

        // Initialize infrastructure builder with parent builder
        Infrastructure = new InfrastructureBuilder(this);

        // Initialize remaining feature builders
        Api = new EndatixApiBuilder(this);
        Persistence = new EndatixPersistenceBuilder(this);
        Security = new EndatixSecurityBuilder(this);
        Messaging = new EndatixMessagingBuilder(this);
    }

    /// <summary>
    /// Configures Endatix with default settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixBuilder UseDefaults()
    {
        Logging.UseDefaults();

        SetupLogger.Information("Starting Endatix configuration with default settings");

        var databaseProvider = GetConfiguredDatabaseProvider();
        Persistence.UseDefaults(databaseProvider);
        SetupLogger.Information($"Persistence configuration completed using {databaseProvider}");

        Infrastructure.UseDefaults();
        SetupLogger.Information("Infrastructure configuration completed");

        Api.UseDefaults();
        SetupLogger.Information("API configuration completed");

        Security.UseDefaults();
        SetupLogger.Information("Security configuration completed");

        SetupLogger.Information("Endatix configuration completed successfully");
        return this;
    }

    /// <summary>
    /// Attempts to get the configured database provider from configuration.
    /// Defaults to SQL Server if not specified.
    /// </summary>
    private DatabaseProvider GetConfiguredDatabaseProvider()
    {
        var providerName = Configuration.GetConnectionString("DefaultConnection_DbProvider")?.ToLowerInvariant();

        if (!string.IsNullOrEmpty(providerName) &&
            Enum.TryParse<DatabaseProvider>(providerName, true, out var provider))
        {
            return provider;
        }

        return DatabaseProvider.SqlServer;
    }

    /// <summary>
    /// Configures Endatix with minimal settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixBuilder UseMinimalSetup()
    {
        // Configure only essential services
        var databaseProvider = GetConfiguredDatabaseProvider();
        Persistence.UseDefaults(databaseProvider);
        SetupLogger.Information($"Minimal setup completed with {databaseProvider} persistence");

        return this;
    }

    /// <summary>
    /// Configures Endatix options.
    /// </summary>
    /// <param name="configure">Action to configure options.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixBuilder ConfigureOptions(Action<EndatixOptions> configure)
    {
        Services.Configure(configure);
        return this;
    }

    #region Persistence Convenience Methods

    /// <summary>
    /// Configures SQL Server persistence with default settings.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public EndatixBuilder UseSqlServer<TContext>() where TContext : DbContext
    {
        Persistence.UseSqlServer<TContext>();

        // Also register the identity context if not already registered
        if (typeof(TContext) != typeof(AppIdentityDbContext))
        {
            Persistence.UseSqlServer<AppIdentityDbContext>();
        }

        return this;
    }

    /// <summary>
    /// Configures SQL Server persistence with custom options.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="configAction">Action to configure SQL Server options.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixBuilder UseSqlServer<TContext>(Action<object> configAction) where TContext : DbContext
    {
        Persistence.UseSqlServer<TContext>(configAction);
        return this;
    }

    /// <summary>
    /// Configures PostgreSQL persistence with default settings.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public EndatixBuilder UsePostgreSql<TContext>() where TContext : DbContext
    {
        Persistence.UsePostgreSql<TContext>();

        // Also register the identity context if not already registered
        if (typeof(TContext) != typeof(AppIdentityDbContext))
        {
            Persistence.UsePostgreSql<AppIdentityDbContext>();
        }

        return this;
    }

    /// <summary>
    /// Configures PostgreSQL persistence with custom options.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="configAction">Action to configure PostgreSQL options.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixBuilder UsePostgreSql<TContext>(Action<object> configAction) where TContext : DbContext
    {
        Persistence.UsePostgreSql<TContext>(configAction);
        return this;
    }

    /// <summary>
    /// Enables auto migrations for the database.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixBuilder EnableAutoMigrations()
    {
        Persistence.EnableAutoMigrations();
        return this;
    }

    /// <summary>
    /// Scans the specified assemblies for entity configurations.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixBuilder ScanAssembliesForEntities(params Assembly[] assemblies)
    {
        Persistence.ScanAssembliesForEntities(assemblies);
        return this;
    }

    #endregion
}