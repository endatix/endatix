using Endatix.Persistence.PostgreSql.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Persistence.PostgreSql.Builders;

/// <summary>
/// Builder for configuring PostgreSQL persistence.
/// </summary>
public class PostgreSqlPersistenceBuilder
{
    private readonly IServiceCollection _services;
    private readonly ILogger? _logger;
    
    /// <summary>
    /// Initializes a new instance of the PostgreSqlPersistenceBuilder class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="loggerFactory">The optional logger factory.</param>
    public PostgreSqlPersistenceBuilder(IServiceCollection services, ILoggerFactory? loggerFactory = null)
    {
        _services = services;
        _logger = loggerFactory?.CreateLogger("Endatix.Setup");
    }
    
    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services => _services;
    
    /// <summary>
    /// Configures the PostgreSQL persistence with default settings from DataOptions.
    /// </summary>
    /// <typeparam name="TContext">The DB context type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public PostgreSqlPersistenceBuilder UseDefault<TContext>() 
        where TContext : DbContext
    {
        LogSetupInfo($"Configuring PostgreSQL persistence for {typeof(TContext).Name} with default settings");
        
        _services.AddDbContext<TContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetService<IConfiguration>();
            var connectionString = configuration?.GetConnectionString("DefaultConnection");
            
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("DefaultConnection string is not configured in the application configuration.");
            }
            
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(TContext).Assembly.GetName().Name);
                npgsqlOptions.EnableRetryOnFailure(5);
            });
        });
        
        LogSetupInfo($"PostgreSQL persistence for {typeof(TContext).Name} configured successfully");
        return this;
    }
    
    /// <summary>
    /// Configures the PostgreSQL persistence with custom options.
    /// </summary>
    /// <typeparam name="TContext">The DB context type.</typeparam>
    /// <param name="options">The custom options configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public PostgreSqlPersistenceBuilder Configure<TContext>(Action<PostgreSqlOptions> options) 
        where TContext : DbContext
    {
        LogSetupInfo($"Configuring PostgreSQL persistence for {typeof(TContext).Name} with custom settings");
        
        var postgresOptions = new PostgreSqlOptions();
        options(postgresOptions);
        
        _services.AddDbContext<TContext>((serviceProvider, dbContextOptions) =>
        {
            dbContextOptions.UseNpgsql(postgresOptions.ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(postgresOptions.MigrationsAssembly ?? typeof(TContext).Assembly.GetName().Name);
                
                if (postgresOptions.CommandTimeout.HasValue)
                {
                    npgsqlOptions.CommandTimeout(postgresOptions.CommandTimeout.Value);
                }
                
                npgsqlOptions.EnableRetryOnFailure(
                    postgresOptions.MaxRetryCount,
                    TimeSpan.FromSeconds(postgresOptions.MaxRetryDelay),
                    null);
            });
            
            if (postgresOptions.EnableSensitiveDataLogging)
            {
                dbContextOptions.EnableSensitiveDataLogging();
            }
            
            if (postgresOptions.EnableDetailedErrors)
            {
                dbContextOptions.EnableDetailedErrors();
            }
        });
        
        LogSetupInfo($"PostgreSQL persistence for {typeof(TContext).Name} configured successfully");
        return this;
    }
    
    private void LogSetupInfo(string message)
    {
        _logger?.LogInformation("[💿 PostgreSQL Setup] {Message}", message);
    }
} 