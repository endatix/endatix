using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    /// </summary>
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

        // Register core framework services (including IAppEnvironment)
        services.AddEndatixFrameworkServices();

        // Register core identity services that are needed regardless of configuration method
        services.AddEndatixIdentityEssentialServices();

        // Create and return the builder
        return new EndatixBuilder(services, configuration);
    }

    /// <summary>
    /// Adds Endatix services with default configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEndatixWithDefaults(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddEndatix(configuration)
            .UseDefaults();

        return services;
    }

    /// <summary>
    /// Adds Endatix services with SQL Server persistence.
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

        // Also register the identity context if not already registered
        if (typeof(TContext) != typeof(AppIdentityDbContext))
        {
            builder.Persistence.UseSqlServer<AppIdentityDbContext>();
        }

        return builder;
    }

    /// <summary>
    /// Adds Endatix services with PostgreSQL persistence.
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

        // Also register the identity context if not already registered
        if (typeof(TContext) != typeof(AppIdentityDbContext))
        {
            builder.Persistence.UsePostgreSql<AppIdentityDbContext>();
        }

        return builder;
    }
}