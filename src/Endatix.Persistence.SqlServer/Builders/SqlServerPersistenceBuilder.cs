using Endatix.Infrastructure.Data;
using Endatix.Persistence.SqlServer.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Persistence.SqlServer.Builders;

/// <summary>
/// Builder for configuring SQL Server persistence.
/// </summary>
public class SqlServerPersistenceBuilder
{
    private readonly IServiceCollection _services;
    private readonly ILogger? _logger;
    
    /// <summary>
    /// Initializes a new instance of the SqlServerPersistenceBuilder class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="loggerFactory">The optional logger factory.</param>
    public SqlServerPersistenceBuilder(IServiceCollection services, ILoggerFactory? loggerFactory = null)
    {
        _services = services;
        _logger = loggerFactory?.CreateLogger("Endatix.Setup");
    }
    
    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services => _services;
    
    /// <summary>
    /// Configures the SQL Server persistence with default settings from DataOptions.
    /// </summary>
    /// <typeparam name="TContext">The DB context type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public SqlServerPersistenceBuilder UseDefault<TContext>() 
        where TContext : DbContext
    {
        LogSetupInfo($"Configuring SQL Server persistence for {typeof(TContext).Name} with default settings");
        
        _services.AddDbContext<TContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetService<IConfiguration>();
            var connectionString = configuration?.GetConnectionString("DefaultConnection");
            
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("DefaultConnection string is not configured in the application configuration.");
            }
            
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(TContext).Assembly.GetName().Name);
                sqlOptions.EnableRetryOnFailure();
            });
        });
        
        LogSetupInfo($"SQL Server persistence for {typeof(TContext).Name} configured successfully");
        return this;
    }
    
    /// <summary>
    /// Configures the SQL Server persistence with custom options.
    /// </summary>
    /// <typeparam name="TContext">The DB context type.</typeparam>
    /// <param name="options">The custom options configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public SqlServerPersistenceBuilder Configure<TContext>(Action<SqlServerOptions> options) 
        where TContext : DbContext
    {
        LogSetupInfo($"Configuring SQL Server persistence for {typeof(TContext).Name} with custom settings");
        
        var sqlServerOptions = new SqlServerOptions();
        options(sqlServerOptions);
        
        _services.AddDbContext<TContext>((serviceProvider, dbContextOptions) =>
        {
            dbContextOptions.UseSqlServer(sqlServerOptions.ConnectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(sqlServerOptions.MigrationsAssembly ?? typeof(TContext).Assembly.GetName().Name);
                
                if (sqlServerOptions.CommandTimeout.HasValue)
                {
                    sqlOptions.CommandTimeout(sqlServerOptions.CommandTimeout.Value);
                }
                
                sqlOptions.EnableRetryOnFailure(
                    sqlServerOptions.MaxRetryCount,
                    TimeSpan.FromSeconds(sqlServerOptions.MaxRetryDelay),
                    null);
            });
            
            if (sqlServerOptions.EnableSensitiveDataLogging)
            {
                dbContextOptions.EnableSensitiveDataLogging();
            }
            
            if (sqlServerOptions.EnableDetailedErrors)
            {
                dbContextOptions.EnableDetailedErrors();
            }
        });
        
        LogSetupInfo($"SQL Server persistence for {typeof(TContext).Name} configured successfully");
        return this;
    }
    
    private void LogSetupInfo(string message)
    {
        _logger?.LogInformation("[SQL Server Setup] {Message}", message);
    }
} 