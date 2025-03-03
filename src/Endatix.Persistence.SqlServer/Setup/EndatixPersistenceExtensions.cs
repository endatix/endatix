using Endatix.Persistence.SqlServer.Builders;
using Endatix.Persistence.SqlServer.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Persistence.SqlServer.Setup;

/// <summary>
/// Extension methods for configuring SQL Server persistence.
/// </summary>
public static class EndatixPersistenceExtensions
{
    /// <summary>
    /// Adds SQL Server database persistence with default settings.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSqlServerPersistence<TContext>(
        this IServiceCollection services,
        ILoggerFactory? loggerFactory = null)
        where TContext : DbContext
    {
        var builder = new SqlServerPersistenceBuilder(services, loggerFactory);
        return builder.UseDefault<TContext>().Services;
    }
    
    /// <summary>
    /// Adds SQL Server database persistence with custom options.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsAction">Action to configure SQL Server options.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSqlServerPersistence<TContext>(
        this IServiceCollection services,
        Action<SqlServerOptions> optionsAction,
        ILoggerFactory? loggerFactory = null)
        where TContext : DbContext
    {
        var builder = new SqlServerPersistenceBuilder(services, loggerFactory);
        return builder.Configure<TContext>(optionsAction).Services;
    }
    
    /// <summary>
    /// Gets a SQL Server persistence builder for further configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>A SQL Server persistence builder.</returns>
    public static SqlServerPersistenceBuilder GetSqlServerBuilder(
        this IServiceCollection services,
        ILoggerFactory? loggerFactory = null)
    {
        return new SqlServerPersistenceBuilder(services, loggerFactory);
    }
} 