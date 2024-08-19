﻿using Endatix.Infrastructure.Data;
using Endatix.Core.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Services;

namespace Endatix.SqlServer
{
    public static class ServiceCollectionExtensions
    {
        public static void AddEndatix(this IServiceCollection services, Action<IEndatixConfig> configuration)
        {
            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("EF SqlServices");

            IEndatixConfig configurationInstance = EndatixConfig.Configuration;

            // Run the configuration callback to apply the provided settings
            configuration(configurationInstance);

            Guard.Against.NullOrEmpty(EndatixConfig.Configuration.ConnectionString, null, "Endatix database connection not provided.");

            string? connectionString = EndatixConfig.Configuration.ConnectionString;
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(connectionString, b => b.MigrationsAssembly("SampleWebApp"));
            });

            if (EndatixConfig.Configuration.UseSnowflakeIds)
            {
                services.AddSingleton<IIdGenerator, SnowflakeIdGenerator>();
            }

            logger.LogInformation("{Project} services registered", "EF SqlServices");
        }
    }
}