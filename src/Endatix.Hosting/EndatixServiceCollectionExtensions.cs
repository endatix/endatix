using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Ardalis.GuardClauses;
using Endatix.Hosting.Builders;
using Endatix.Hosting.Core;
using Endatix.Hosting.Options;
using Microsoft.EntityFrameworkCore;
using Endatix.Infrastructure.Identity;
using Endatix.Framework.Hosting;
using Endatix.Framework.Setup;

namespace Endatix.Hosting;

/// <summary>
/// Extension methods for configuring Endatix services.
/// </summary>
public static class EndatixServiceCollectionExtensions
{
    /// <summary>
    /// Adds Endatix services to the service collection.
    /// This is the main entry point for configuring Endatix in your application.
    /// </summary>
    /// <remarks>
    /// This method:
    /// 1. Registers core Endatix framework services
    /// 2. Registers core identity services
    /// 3. Provides a fluent builder API for further configuration
    /// 
    /// Use this method for the most flexibility. For convenience, you can use 
    /// AddEndatixWithDefaults, AddEndatixWithSqlServer, or AddEndatixWithPostgreSql
    /// for common scenarios.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>An EndatixBuilder for further configuration.</returns>
    public static EndatixBuilder AddEndatix(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        Guard.Against.Null(services);
        Guard.Against.Null(configuration);

        // Register options from configuration
        services.Configure<EndatixOptions>(
            configuration.GetSection("Endatix"));
        services.Configure<HostingOptions>(
            configuration.GetSection(HostingOptions.SectionName));

        // Register core framework services (this handles IAppEnvironment registration)
        services.AddEndatixFrameworkServices();

        // Register core identity services
        services.AddEndatixIdentityEssentialServices();

        // Create the builder
        return new EndatixBuilder(services, configuration);
    }

    /// <summary>
    /// Adds Endatix services with default settings including SQL Server persistence.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection with Endatix configured.</returns>
    public static IServiceCollection AddEndatixWithDefaults(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Use automatic database provider selection based on configuration
        var builder = services.AddEndatix(configuration);
        builder.UseDefaults();
        return services;
    }

    /// <summary>
    /// Adds Endatix services with SQL Server as the default database for the specified context.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The EndatixBuilder for further configuration.</returns>
    public static EndatixBuilder AddEndatixWithSqlServer<TContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TContext : DbContext
    {
        var builder = services.AddEndatix(configuration);
        builder.Persistence.UseSqlServer<TContext>();

        // Register AppIdentityDbContext if it's not already registered 
        // and if TContext is not already AppIdentityDbContext
        if (!typeof(TContext).Name.Equals(nameof(AppIdentityDbContext)))
        {
            builder.Persistence.UseSqlServer<AppIdentityDbContext>();
        }

        return builder;
    }

    /// <summary>
    /// Adds Endatix services with PostgreSQL as the default database for the specified context.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The EndatixBuilder for further configuration.</returns>
    public static EndatixBuilder AddEndatixWithPostgreSql<TContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TContext : DbContext
    {
        var builder = services.AddEndatix(configuration);
        builder.Persistence.UsePostgreSql<TContext>();

        // Register AppIdentityDbContext if it's not already registered 
        // and if TContext is not already AppIdentityDbContext
        if (!typeof(TContext).Name.Equals(nameof(AppIdentityDbContext)))
        {
            builder.Persistence.UsePostgreSql<AppIdentityDbContext>();
        }

        return builder;
    }
}