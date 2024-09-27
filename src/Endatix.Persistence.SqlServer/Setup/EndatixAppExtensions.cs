using System.Reflection;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Configuration;
using Endatix.Framework.Hosting;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Setup;

/// <summary>
/// Provides extension methods for configuring data persistence in the Endatix application.
/// </summary>
public static class EndatixAppExtensions
{
    /// <summary>
    /// Adds data persistence to the specified <see cref="IEndatixApp"/> instance, configuring the application's database and optional Snowflake ID generation.
    /// </summary>
    /// <param name="endatixApp">The <see cref="IEndatixApp"/> instance to configure.</param>
    /// <param name="configuration">A delegate to configure the <see cref="IEndatixConfig"/> instance</param>
    /// <returns>The configured <see cref="IEndatixApp"/> instance.</returns>
    public static IEndatixApp AddDataPersistence(this IEndatixApp endatixApp, Action<IEndatixConfig> configuration)
    {
        IEndatixConfig configurationInstance = EndatixConfig.Configuration;
        configuration(configurationInstance);

        Guard.Against.NullOrEmpty(EndatixConfig.Configuration.ConnectionString, null, "Endatix database connection not provided. Make sure to call WithSqlServer method and pass the connection string");

        string? connectionString = EndatixConfig.Configuration.ConnectionString;
        string? migrationsAssembly = EndatixConfig.Configuration.MigrationsAssembly ?? Assembly.GetExecutingAssembly().GetName().Name;

        endatixApp.Services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(connectionString, db => db.MigrationsAssembly(migrationsAssembly));
        });

        endatixApp.Services.AddDbContext<AppIdentityDbContext>(options =>
        {
            options.UseSqlServer(connectionString, db => db.MigrationsAssembly(migrationsAssembly));
        });

        if (EndatixConfig.Configuration.UseSnowflakeIds)
        {
            endatixApp.Services.AddSingleton<IIdGenerator<long>, SnowflakeIdGenerator>();
        }

        endatixApp.LogSetupInformation("Persistence using SqlServer configured");

        return endatixApp;
    }
}
