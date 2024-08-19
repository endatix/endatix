using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Configuration;
using Endatix.Framework.Hosting;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Setup;

public static class EndatixWebAppExtensions
{
    public static IEndatixApp UseSqlServer(this IEndatixApp endatixApp, Action<IEndatixConfig> configuration)
    {
        IEndatixConfig configurationInstance = EndatixConfig.Configuration;
        configuration(configurationInstance);

        Guard.Against.NullOrEmpty(EndatixConfig.Configuration.ConnectionString, null, "Endatix database connection not provided.");

        string? connectionString = EndatixConfig.Configuration.ConnectionString;
        endatixApp.Services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(connectionString, b => b.MigrationsAssembly("SampleWebApp"));
        });

        if (EndatixConfig.Configuration.UseSnowflakeIds)
        {
            endatixApp.Services.AddSingleton<IIdGenerator, SnowflakeIdGenerator>();
        }

        endatixApp.LogBuilderInformation("Persistence using SqlServer configured");

        return endatixApp;
    }
}
