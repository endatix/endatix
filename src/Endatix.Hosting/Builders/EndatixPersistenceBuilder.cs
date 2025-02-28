using System;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Builder for configuring Endatix data persistence.
/// </summary>
public class EndatixPersistenceBuilder
{
    private readonly EndatixBuilder _parentBuilder;
    
    /// <summary>
    /// Initializes a new instance of the EndatixPersistenceBuilder class.
    /// </summary>
    /// <param name="parentBuilder">The parent builder.</param>
    internal EndatixPersistenceBuilder(EndatixBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
    }
    
    /// <summary>
    /// Configures persistence with default settings.
    /// </summary>
    /// <returns>The persistence builder for chaining.</returns>
    public EndatixPersistenceBuilder UseDefaults()
    {
        var dbProvider = _parentBuilder.Configuration.GetConnectionString("DefaultConnection_DbProvider")?.ToLowerInvariant() ?? "sqlserver";
        var connectionString = _parentBuilder.Configuration.GetConnectionString("DefaultConnection");
        
        switch (dbProvider)
        {
            case "postgresql":
                UseSqlPostgres(options => options
                    .WithConnectionString(connectionString)
                    .WithDefaultConfiguration());
                break;
            case "sqlserver":
                UseSqlServer(options => options
                    .WithConnectionString(connectionString)
                    .WithDefaultConfiguration());
                break;
            default:
                throw new ArgumentException($"Unsupported database provider: {dbProvider}. Supported values are 'sqlserver' and 'postgresql'");
        }
        
        return this;
    }
    
    /// <summary>
    /// Configures SQL Server persistence.
    /// </summary>
    /// <param name="configure">Action to configure database options.</param>
    /// <returns>The persistence builder for chaining.</returns>
    public EndatixPersistenceBuilder UseSqlServer(Action<SqlServerOptions> configure)
    {
        var options = new SqlServerOptions();
        configure(options);
        
        // Register SQL Server services
        
        return this;
    }
    
    /// <summary>
    /// Configures PostgreSQL persistence.
    /// </summary>
    /// <param name="configure">Action to configure database options.</param>
    /// <returns>The persistence builder for chaining.</returns>
    public EndatixPersistenceBuilder UseSqlPostgres(Action<PostgresOptions> configure)
    {
        var options = new PostgresOptions();
        configure(options);
        
        // Register PostgreSQL services
        
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
    /// Gets the parent builder.
    /// </summary>
    /// <returns>The parent builder.</returns>
    public EndatixBuilder Parent() => _parentBuilder;
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
        // Configure Snowflake IDs
        return this;
    }
    
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