using System.Reflection;
using Endatix.Framework.Hosting;
using Endatix.Infrastructure.Builders;
using Endatix.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Main builder for configuring Endatix services. This is the central hub that provides
/// access to all specialized builders for different aspects of the application.
/// </summary>
/// <remarks>
/// The EndatixBuilder provides a fluent API for configuring various aspects of your Endatix application:
/// <list type="bullet">
/// <item><description>API configuration through <see cref="Api"/></description></item>
/// <item><description>Persistence configuration through <see cref="Persistence"/></description></item>
/// <item><description>Security configuration through <see cref="Security"/></description></item>
/// <item><description>Messaging configuration through <see cref="Messaging"/></description></item>
/// <item><description>Logging configuration through <see cref="Logging"/></description></item>
/// <item><description>Infrastructure configuration through <see cref="Infrastructure"/></description></item>
/// </list>
/// 
/// Typically, you obtain an instance of this builder by calling one of the extension methods
/// in <see cref="EndatixHostBuilderExtensions"/>, such as <c>UseEndatix</c>.
/// </remarks>
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
    /// Gets the API builder for configuring API-related services such as routing, 
    /// versioning, Swagger, and CORS policies.
    /// </summary>
    public EndatixApiBuilder Api { get; }

    /// <summary>
    /// Gets the persistence builder for configuring database contexts, 
    /// migrations, and other data storage related services.
    /// </summary>
    public EndatixPersistenceBuilder Persistence { get; }

    /// <summary>
    /// Gets the security builder for configuring authentication, 
    /// authorization, and other security-related services.
    /// </summary>
    public EndatixSecurityBuilder Security { get; }

    /// <summary>
    /// Gets the messaging builder.
    /// </summary>
    public EndatixMessagingBuilder Messaging { get; }

    /// <summary>
    /// Gets the logging builder.
    /// </summary>
    public EndatixLoggingBuilder Logging => _loggingBuilder!;

    /// <summary>
    /// Gets a logger factory that can create loggers for specific categories.
    /// </summary>
    public ILoggerFactory LoggerFactory { get; private set; }

    /// <summary>
    /// Gets the infrastructure builder.
    /// </summary>
    public InfrastructureBuilder Infrastructure { get; }

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
        Infrastructure = new InfrastructureBuilder(this);
        Api = new EndatixApiBuilder(this);
        Persistence = new EndatixPersistenceBuilder(this);
        Security = new EndatixSecurityBuilder(this);
        Messaging = new EndatixMessagingBuilder(this);

        _logger.LogInformation("EndatixBuilder initialized successfully");
    }

    /// <summary>
    /// Configures Endatix with defaults. This is the recommended way to use Endatix for most applications.
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

        Security.UseDefaults();
        _logger.LogInformation("Security configuration completed");

        _logger.LogInformation("Endatix configuration completed successfully");
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
        _logger.LogInformation("Minimal setup completed with {DatabaseProvider} persistence", databaseProvider);

        return this;
    }

    #region Persistence Convenience Methods

    /// <summary>
    /// Configures SQL Server persistence with default settings.
    /// </summary>
    /// <remarks>
    /// This is a convenience method that configures both your specified database context and the 
    /// identity context to use SQL Server. It automatically resolves the connection string from 
    /// configuration using the "DefaultConnection" key.
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Register Endatix with SQL Server
    /// builder.Host.UseEndatix(endatix => endatix
    ///     .UseSqlServer&lt;AppDbContext&gt;()
    ///     .EnableAutoMigrations());
    /// </code>
    /// </example>
    /// </remarks>
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
    /// <remarks>
    /// This method allows you to provide custom configuration for SQL Server through the configAction parameter.
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Register Endatix with SQL Server
    /// builder.Host.UseEndatix(endatix => endatix
    ///     .UseSqlServer&lt;AppDbContext&gt;(options => 
    ///     {
    ///         options.WithConnectionString("Server=myServer;Database=myDb;Trusted_Connection=True;");
    ///         options.WithSnowflakeIds(1);
    ///     })
    ///     .EnableAutoMigrations());
    /// </code>
    /// </example>
    /// </remarks>
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
    /// <remarks>
    /// This is a convenience method that configures both your specified database context and the 
    /// identity context to use PostgreSQL. It automatically resolves the connection string from 
    /// configuration using the "DefaultConnection" key.
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Register Endatix with PostgreSQL
    /// builder.Host.UseEndatix(endatix => endatix
    ///     .UsePostgreSql&lt;AppDbContext&gt;()
    ///     .EnableAutoMigrations());
    /// </code>
    /// </example>
    /// </remarks>
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
    /// <remarks>
    /// This method allows you to provide custom configuration for PostgreSQL through the configAction parameter.
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Register Endatix with custom PostgreSQL options
    /// builder.Host.UseEndatix(endatix => endatix
    ///     .UsePostgreSql&lt;AppDbContext&gt;(options => 
    ///     {
    ///         options.WithConnectionString("Host=localhost;Database=mydb;Username=postgres;Password=password");
    ///         options.WithCustomTablePrefix("app_");
    ///     })
    ///     .EnableAutoMigrations());
    /// </code>
    /// </example>
    /// </remarks>
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
    /// <remarks>
    /// When enabled, database migrations will be automatically applied at application startup.
    /// This is useful for development environments, but should be used with caution in production.
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Register Endatix with auto migrations
    /// builder.Host.UseEndatix(endatix => endatix
    ///     .UseSqlServer&lt;AppDbContext&gt;()
    ///     .EnableAutoMigrations());
    /// </code>
    /// </example>
    /// </remarks>
    /// <returns>The builder for chaining.</returns>
    public EndatixBuilder EnableAutoMigrations()
    {
        Persistence.EnableAutoMigrations();
        return this;
    }

    /// <summary>
    /// Scans the specified assemblies for entity configurations.
    /// </summary>
    /// <remarks>
    /// This method scans the specified assemblies for Entity Framework entity type configurations that 
    /// implement IEntityTypeConfiguration&lt;T&gt;.
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Register Endatix and scan for entity configurations
    /// builder.Host.UseEndatix(endatix => endatix
    ///     .UseSqlServer&lt;AppDbContext&gt;()
    ///     .ScanAssembliesForEntities(typeof(Program).Assembly));
    /// </code>
    /// </example>
    /// </remarks>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixBuilder ScanAssembliesForEntities(params Assembly[] assemblies)
    {
        Persistence.ScanAssembliesForEntities(assemblies);
        return this;
    }

    #endregion
}