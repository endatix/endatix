using System.Reflection;
using Endatix.Framework.Hosting;
using Endatix.Framework.Setup;
using Endatix.Hosting.Logging;
using Endatix.Hosting.Options;
using Endatix.Infrastructure.Builders;
using Endatix.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
/// in <see cref="EndatixServiceCollectionExtensions"/>, such as <c>AddEndatix</c>.
/// </remarks>
public class EndatixBuilder : IBuilderRoot
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
                Logging.UseDefaults();
            }

            if (Logging.LoggerFactory == null)
            {
                throw new InvalidOperationException("Logger factory could not be initialized. Please check your logging configuration.");
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

        // Initialize and configure logging builder first to ensure logger factory is available
        Logging = new EndatixLoggingBuilder(this);
        Logging.UseDefaults();

        // Initialize infrastructure builder with root builder
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
    /// <remarks>
    /// This method:
    /// <list type="number">
    /// <item><description>Configures logging with default settings</description></item>
    /// <item><description>Sets up persistence based on the detected database provider</description></item>
    /// <item><description>Configures infrastructure with default settings</description></item>
    /// <item><description>Sets up API with default settings</description></item>
    /// <item><description>Configures security with default settings</description></item>
    /// </list>
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Add Endatix services with defaults
    /// builder.Services.AddEndatix(builder.Configuration)
    ///     .UseDefaults();
    ///     
    /// var app = builder.Build();
    /// 
    /// app.UseEndatix();
    /// app.Run();
    /// </code>
    /// </example>
    /// </remarks>
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
    /// builder.Services.AddEndatix(builder.Configuration)
    ///     .UseSqlServer&lt;AppDbContext&gt;()
    ///     .EnableAutoMigrations();
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
    /// // Register Endatix with custom SQL Server options
    /// builder.Services.AddEndatix(builder.Configuration)
    ///     .UseSqlServer&lt;AppDbContext&gt;(options => 
    ///     {
    ///         options.WithConnectionString("Server=myServer;Database=myDb;Trusted_Connection=True;");
    ///         options.WithSnowflakeIds(1);
    ///     })
    ///     .EnableAutoMigrations();
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
    /// builder.Services.AddEndatix(builder.Configuration)
    ///     .UsePostgreSql&lt;AppDbContext&gt;()
    ///     .EnableAutoMigrations();
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
    /// builder.Services.AddEndatix(builder.Configuration)
    ///     .UsePostgreSql&lt;AppDbContext&gt;(options => 
    ///     {
    ///         options.WithConnectionString("Host=localhost;Database=mydb;Username=postgres;Password=password");
    ///         options.WithCustomTablePrefix("app_");
    ///     })
    ///     .EnableAutoMigrations();
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
    /// builder.Services.AddEndatix(builder.Configuration)
    ///     .UseSqlServer&lt;AppDbContext&gt;()
    ///     .EnableAutoMigrations();
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
    /// builder.Services.AddEndatix(builder.Configuration)
    ///     .UseSqlServer&lt;AppDbContext&gt;()
    ///     .ScanAssembliesForEntities(typeof(Program).Assembly);
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