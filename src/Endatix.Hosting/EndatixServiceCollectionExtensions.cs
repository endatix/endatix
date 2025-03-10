using Ardalis.GuardClauses;
using Endatix.Framework.Configuration;
using Endatix.Framework.Setup;
using Endatix.Hosting.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Hosting;

/// <summary>
/// Extension methods for configuring Endatix services.
/// </summary>
public static class EndatixServiceCollectionExtensions
{
    /// <summary>
    /// Adds Endatix services to the service collection. This is the entry point for configuring Endatix.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>A builder for further configuration.</returns>
    public static EndatixBuilder AddEndatix(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        Guard.Against.Null(services);
        Guard.Against.Null(configuration);

        // Register core framework services FIRST to ensure IAppEnvironment is available∏∏
        RegisterCoreFrameworkServices(services, configuration);

        // Register standard Endatix options
        services.AddStandardEndatixOptions(configuration);

        // Now create the logging builder which will get environment from services
        var loggingBuilder = new EndatixLoggingBuilder(services, configuration);
        var loggerFactory = loggingBuilder.GetComponents();

        // Create the main builder with logging already configured
        var builder = new EndatixBuilder(services, configuration);
        var logger = builder.LoggerFactory.CreateLogger(typeof(EndatixServiceCollectionExtensions));

        // Register remaining services
        logger.LogInformation("Registering identity services");
        RegisterIdentityServices(services, configuration);

        return builder;
    }

    /// <summary>
    /// Adds Endatix services with default settings including automatic database provider selection.
    /// This is the simplest way to configure Endatix with sensible defaults.
    /// </summary>
    /// <remarks>
    /// <example>
    /// <code>
    /// // In Program.cs:
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Add Endatix with default configuration
    /// builder.Services.AddEndatixWithDefaults(builder.Configuration);
    /// 
    /// var app = builder.Build();
    /// 
    /// app.UseEndatix();
    /// app.Run();
    /// </code>
    /// </example>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection with Endatix configured.</returns>
    public static IServiceCollection AddEndatixWithDefaults(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Get the Endatix builder with core services already set up
        var builder = services.AddEndatix(configuration);
        var logger = builder.LoggerFactory.CreateLogger(typeof(EndatixServiceCollectionExtensions));

        // Apply default configuration to all components
        builder.UseDefaults();

        logger.LogInformation("Endatix configured with default settings");
        return builder.Services;
    }

    /// <summary>
    /// Adds Endatix services with SQL Server as the default database for the specified context.
    /// </summary>
    /// <remarks>
    /// <example>
    /// <code>
    /// // In Program.cs:
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Add Endatix with SQL Server
    /// var endatixBuilder = builder.Services.AddEndatixWithSqlServer&lt;AppDbContext&gt;(builder.Configuration);
    /// 
    /// // Additional configuration as needed
    /// endatixBuilder.Api.AddSwagger();
    /// 
    /// var app = builder.Build();
    /// 
    /// app.UseEndatix();
    /// app.Run();
    /// </code>
    /// </example>
    /// </remarks>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The EndatixBuilder for further configuration.</returns>
    public static EndatixBuilder AddEndatixWithSqlServer<TContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TContext : DbContext
    {
        // Create core services
        var builder = services.AddEndatix(configuration);
        builder.LogSetupInfo("Configuring Endatix with SQL Server...");

        // Register the configured logger
        builder.Logging.RegisterConfiguredLogger();

        // Configure with SQL Server
        builder.UseSqlServer<TContext>();

        builder.LogSetupInfo("Endatix configured with SQL Server");
        return builder;
    }

    /// <summary>
    /// Adds Endatix services with PostgreSQL as the default database for the specified context.
    /// </summary>
    /// <remarks>
    /// <example>
    /// <code>
    /// // In Program.cs:
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Add Endatix with PostgreSQL
    /// var endatixBuilder = builder.Services.AddEndatixWithPostgreSql&lt;AppDbContext&gt;(builder.Configuration);
    /// 
    /// // Additional configuration as needed
    /// endatixBuilder.Security.UseJwtAuthentication();
    /// 
    /// var app = builder.Build();
    /// 
    /// app.UseEndatix();
    /// app.Run();
    /// </code>
    /// </example>
    /// </remarks>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The EndatixBuilder for further configuration.</returns>
    public static EndatixBuilder AddEndatixWithPostgreSql<TContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TContext : DbContext
    {
        // Create core services
        var builder = services.AddEndatix(configuration);
        builder.LogSetupInfo("Configuring Endatix with PostgreSQL...");

        // Configure with PostgreSQL
        builder.UsePostgreSql<TContext>();

        builder.LogSetupInfo("Endatix configured with PostgreSQL");
        return builder;
    }

    private static void RegisterCoreFrameworkServices(IServiceCollection services, IConfiguration configuration)
    {
        // Use the existing framework service registration that includes IAppEnvironment
        services.AddEndatixFrameworkServices();

        // Add additional core services as needed
        services.AddOptions();
        services.AddHealthChecks();
    }

    private static void RegisterIdentityServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register identity services
    }
}