using System.Reflection;
using Ardalis.Specification;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Infrastructure.Data.Querying;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Outbox.Engine;
using Endatix.Persistence.SqlServer.Options;
using Endatix.Persistence.SqlServer.Querying;
using Endatix.Persistence.SqlServer.Repositories;
using Microsoft.Data.SqlClient;
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

    // Resolves the connection string the active DbContext uses, so the outbox claim store polls the SAME
    // database the app writes outbox rows to. Defaults to ConnectionStrings:DefaultConnection (the
    // UseDefault path); Configure&lt;TContext&gt; overrides it with the supplied SqlServerOptions.ConnectionString.
    private Func<IServiceProvider, string?> _connectionStringResolver =
        sp => sp.GetService<IConfiguration>()?.GetConnectionString("DefaultConnection");

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
                sqlOptions.UseCompatibilityLevel(170); // SQL Server 2025
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

        // Point the outbox claim store at the same connection string this DbContext uses (not DefaultConnection).
        _connectionStringResolver = _ => sqlServerOptions.ConnectionString;

        Services.AddDbContext<TContext>((serviceProvider, dbContextOptions) =>
        {
            dbContextOptions.UseSqlServer(sqlServerOptions.ConnectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(sqlServerOptions.MigrationsAssembly ?? Assembly.GetExecutingAssembly().GetName().Name);
                sqlOptions.UseCompatibilityLevel(170); // SQL Server 2025

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

    /// <summary>
    /// Registers SQL Server-specific repositories.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public SqlServerPersistenceBuilder AddDbSpecificRepositories()
    {
        Services.AddScoped<IRelationalSubstringLikeFilter, SqlServerSubstringLikeFilter>();
        // submitterProfile filters are PostgreSQL-only in the MVP; this guard fails fast instead of silently ignoring them.
        Services.AddScoped<IEvaluator, SubmitterProfileFilterEvaluator>();
        Services.AddScoped<ISubmissionExportRepository, SubmissionExportRepository>();
        Services.AddScoped<IStorageStatsRepository, StorageStatsRepository>();

        // The engine's raw-ADO.NET outbox claim store. Builds an unopened connection from the SAME
        // connection string the DbContext uses (DefaultConnection by default, or the Configure<TContext>
        // override), so the relay polls the right database and shares the SqlClient provider pool.
        var connectionStringResolver = _connectionStringResolver;
        Services.AddSqlOutboxClaimStore(
            OutboxSqlDialect.SqlServer,
            sp => new SqlConnection(connectionStringResolver(sp)),
            OutboxSchema.DefaultTable);

        // Register the in-process relay here (not in Infrastructure) so it is co-located with the claim store
        // it hard-depends on — the relay exists iff a DB provider is configured. Inert until Phase 3b raises
        // the slice events (the loop ticks but the outbox stays empty).
        Services.AddEndatixOutboxRelay();

        return this;
    }


    private void LogSetupInfo(string message)
    {
        _logger?.LogDebug($" ❯ {{Category}}: {message} ✔️", "💿 Database Setup");
    }
}