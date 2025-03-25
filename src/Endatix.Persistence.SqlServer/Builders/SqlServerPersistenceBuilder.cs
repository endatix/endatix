using System.Reflection;
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
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the SqlServerPersistenceBuilder class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="loggerFactory">The optional logger factory.</param>
    public SqlServerPersistenceBuilder(IServiceCollection services, ILoggerFactory? loggerFactory = null)
    {
        Services = services;
        _logger = loggerFactory?.CreateLogger("Endatix.Setup");
    }

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Configures the SQL Server persistence with default settings from DataOptions.
    /// </summary>
    /// <typeparam name="TContext">The DB context type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public SqlServerPersistenceBuilder UseDefault<TContext>()
        where TContext : DbContext
    {
        Services.AddDbContext<TContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetService<IConfiguration>();
            var connectionString = configuration?.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("DefaultConnection string is not configured in the application configuration.");
            }

            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(Assembly.GetExecutingAssembly().GetName().Name);
            });
        });

        LogSetupInfo($"Persistence for {typeof(TContext).Name} configured successfully");
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
        var sqlServerOptions = new SqlServerOptions();
        options(sqlServerOptions);

        Services.AddDbContext<TContext>((serviceProvider, dbContextOptions) =>
        {
            dbContextOptions.UseSqlServer(sqlServerOptions.ConnectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(sqlServerOptions.MigrationsAssembly ?? Assembly.GetExecutingAssembly().GetName().Name);

                if (sqlServerOptions.CommandTimeout.HasValue)
                {
                    sqlOptions.CommandTimeout(sqlServerOptions.CommandTimeout.Value);
                }
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

        LogSetupInfo($"Persistence for {typeof(TContext).Name} configured successfully");
        return this;
    }

    private void LogSetupInfo(string message)
    {
        _logger?.LogDebug($" ❯ {{Category}}: {message} ✔️", "💿 Database Setup");
    }
}