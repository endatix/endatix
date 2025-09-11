using System.Reflection;
using Endatix.Framework.Hosting;
using Endatix.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Root builder for configuring Endatix services using a fluent builder pattern.
/// Provides specialized builders for API, persistence, infrastructure,
/// logging, and health monitoring components.
/// </summary>
/// <example>
/// // Basic usage
/// builder.Host.ConfigureEndatix();
/// 
/// // Custom configuration
/// builder.Host.ConfigureEndatix(endatix => endatix
///     .UseSqlServer&lt;AppDbContext&gt;()
///     .Api.AddSwagger().Build());
/// </example>
public class EndatixBuilder : IBuilderRoot
{
    private readonly EndatixLoggingBuilder? _loggingBuilder;
    private readonly ILogger<EndatixBuilder> _logger;

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
    /// Builder for API configuration (routing, versioning, documentation).
    /// </summary>
    public EndatixApiBuilder Api { get; }

    /// <summary>
    /// Builder for database configuration (contexts, migrations, data access).
    /// </summary>
    public EndatixPersistenceBuilder Persistence { get; }

    /// <summary>
    /// Builder for logging configuration.
    /// </summary>
    public EndatixLoggingBuilder Logging => _loggingBuilder!;

    /// <summary>
    /// Logger factory for creating category-specific loggers.
    /// </summary>
    public ILoggerFactory LoggerFactory { get; private set; }

    /// <summary>
    /// Builder for infrastructure services (data access, identity, messaging, integrations).
    /// </summary>
    /// <example>
    /// endatix.Infrastructure.Messaging.Configure(options => {
    ///     options.IncludeLoggingPipeline = true;
    /// });
    /// </example>
    public EndatixInfrastructureBuilder Infrastructure { get; }

    /// <summary>
    /// Builder for health monitoring configuration.
    /// </summary>
    public EndatixHealthChecksBuilder HealthChecks { get; }

    /// <summary>
    /// Initializes a new instance of the EndatixBuilder class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    public EndatixBuilder(
        IServiceCollection services,
        IConfiguration configuration)
    {
        Services = services;
        Configuration = configuration;

        // Create and initialize the logging builder
        _loggingBuilder = new EndatixLoggingBuilder(services, configuration);
        LoggerFactory = _loggingBuilder.GetComponents();
        _logger = LoggerFactory.CreateLogger<EndatixBuilder>();

        _logger.LogDebug("Initializing EndatixBuilder");

        // Try to get IAppEnvironment from DI
        var serviceProvider = services.BuildServiceProvider();
        AppEnvironment = serviceProvider.GetService<IAppEnvironment>();

        // Initialize builders
        Infrastructure = new EndatixInfrastructureBuilder(this);
        Api = new EndatixApiBuilder(this);
        Persistence = new EndatixPersistenceBuilder(this);
        HealthChecks = new EndatixHealthChecksBuilder(this);

        _logger.LogInformation("EndatixBuilder initialized successfully");
    }

    /// <summary>
    /// Configures all Endatix components with recommended defaults.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixBuilder UseDefaults()
    {
        // Set up logging with defaults first
        Logging.UseDefaults();

        _logger.LogInformation("Starting Endatix configuration with default settings");

        var databaseProvider = GetConfiguredDatabaseProvider();
        Persistence.UseDefaults(databaseProvider);
        _logger.LogInformation("Persistence configuration completed using {DatabaseProvider}", databaseProvider);

        Infrastructure.UseDefaults();
        _logger.LogInformation("Infrastructure configuration completed");

        Api.UseDefaults();
        _logger.LogInformation("API configuration completed");

        // Configure health checks with default settings
        HealthChecks.UseDefaults();
        _logger.LogInformation("Health checks configuration completed");

        _logger.LogInformation("Endatix configuration completed successfully");
        return this;
    }

    /// <summary>
    /// Gets the configured database provider from configuration or defaults to SQL Server.
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
    /// Configures Endatix with minimal essential settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixBuilder UseMinimalSetup()
    {
        // Configure only essential services
        var databaseProvider = GetConfiguredDatabaseProvider();
        Persistence.UseDefaults(databaseProvider);
        _logger.LogInformation("Minimal setup completed with {DatabaseProvider} persistence", databaseProvider);

        return this;
    }

    #region Persistence Convenience Methods

    /// <summary>
    /// Configures SQL Server persistence with default settings.
    /// Also configures identity context if different from TContext.
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
    /// <example>
    /// .UseSqlServer&lt;AppDbContext&gt;(options => {
    ///     options.WithConnectionString("Server=myServer;Database=myDb;Trusted_Connection=True;");
    /// })
    /// </example>
    public EndatixBuilder UseSqlServer<TContext>(Action<object> configAction) where TContext : DbContext
    {
        Persistence.UseSqlServer<TContext>(configAction);
        return this;
    }

    /// <summary>
    /// Configures PostgreSQL persistence with default settings.
    /// Also configures identity context if different from TContext.
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
    /// <example>
    /// .UsePostgreSql&lt;AppDbContext&gt;(options => {
    ///     options.WithConnectionString("Host=localhost;Database=mydb;Username=postgres;Password=password");
    /// })
    /// </example>
    public EndatixBuilder UsePostgreSql<TContext>(Action<object> configAction) where TContext : DbContext
    {
        Persistence.UsePostgreSql<TContext>(configAction);
        return this;
    }

    /// <summary>
    /// Enables automatic database migrations at application startup.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixBuilder EnableAutoMigrations()
    {
        Persistence.EnableAutoMigrations();
        return this;
    }

    /// <summary>
    /// Scans the specified assemblies for Entity Framework entity configurations.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixBuilder ScanAssembliesForEntities(params Assembly[] assemblies)
    {
        Persistence.ScanAssembliesForEntities(assemblies);
        return this;
    }

    /// <summary>
    /// Finalizes all configurations and applies pending builder configurations.
    /// </summary>
    /// <returns>The service collection for chaining.</returns>
    public IServiceCollection FinalizeConfiguration()
    {
        _logger.LogDebug("Finalizing all configurations");

        // TODO: Add rest of the builders once they are implemented
        Infrastructure.Build();

        _logger.LogInformation("All configurations have been finalized");
        return Services;
    }

    #endregion
}