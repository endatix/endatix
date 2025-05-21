using Endatix.Persistence.PostgreSql.Builders;
using Endatix.Persistence.PostgreSql.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Persistence.PostgreSql.Setup;

/// <summary>
/// Extension methods for configuring PostgreSQL persistence.
/// </summary>
public static class EndatixPersistenceExtensions
{
    /// <summary>
    /// Adds PostgreSQL database persistence with default settings.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPostgreSqlPersistence<TContext>(
        this IServiceCollection services,
        ILoggerFactory? loggerFactory = null)
        where TContext : DbContext
    {
        var builder = new PostgreSqlPersistenceBuilder(services, loggerFactory);
        builder.UseDefault<TContext>()
               .AddDbSpecificRepositories();
        
        return builder.Services;
    }
    
    /// <summary>
    /// Adds PostgreSQL database persistence with custom options.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsAction">Action to configure PostgreSQL options.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPostgreSqlPersistence<TContext>(
        this IServiceCollection services,
        Action<PostgreSqlOptions> optionsAction,
        ILoggerFactory? loggerFactory = null)
        where TContext : DbContext
    {
        var builder = new PostgreSqlPersistenceBuilder(services, loggerFactory);
        builder.Configure<TContext>(optionsAction)
               .AddDbSpecificRepositories();
        
        return builder.Services;
    }
    
    /// <summary>
    /// Gets a PostgreSQL persistence builder for further configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>A PostgreSQL persistence builder.</returns>
    public static PostgreSqlPersistenceBuilder GetPostgreSqlBuilder(
        this IServiceCollection services,
        ILoggerFactory? loggerFactory = null)
    {
        return new PostgreSqlPersistenceBuilder(services, loggerFactory);
    }
} 