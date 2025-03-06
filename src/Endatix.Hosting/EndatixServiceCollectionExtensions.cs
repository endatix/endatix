using Ardalis.GuardClauses;
using Endatix.Framework.Hosting;
using Endatix.Framework.Setup;
using Endatix.Hosting.Builders;
using Endatix.Hosting.Core;
using Endatix.Hosting.Options;
using Endatix.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

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
    /// 
    /// <example>
    /// <code>
    /// // Add Endatix with custom configuration
    /// var builder = services.AddEndatix(configuration);
    /// 
    /// // Configure API features
    /// builder.Api
    ///     .AddSwagger()
    ///     .AddVersioning()
    ///     .EnableCors("AllowedOrigins", cors => 
    ///         cors.WithOrigins("https://example.com")
    ///             .AllowAnyMethod()
    ///             .AllowAnyHeader());
    ///             
    /// // Configure security features
    /// builder.Security
    ///     .UseJwtAuthentication()
    ///     .AddDefaultAuthorization();
    ///     
    /// // Configure persistence features
    /// builder.Persistence
    ///     .UseSqlServer&lt;AppDbContext&gt;()
    ///     .EnableAutoMigrations();
    /// </code>
    /// </example>
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

        // Register core identity services (JWT authentication is now handled exclusively by EndatixSecurityBuilder)
        services.AddEndatixIdentityEssentialServices();

        // Create the builder
        return new EndatixBuilder(services, configuration);
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
        IConfiguration configuration) =>
        services.AddEndatix(configuration)
            .UseDefaults().Services;

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
        var builder = services.AddEndatix(configuration);
        builder.Persistence.UseSqlServer<TContext>();
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
        var builder = services.AddEndatix(configuration);
        builder.Persistence.UsePostgreSql<TContext>();
        return builder;
    }
}