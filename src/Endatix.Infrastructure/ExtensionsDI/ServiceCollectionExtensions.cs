﻿using System;
using Ardalis.GuardClauses;
using FastEndpoints.Security;
using Microsoft.Extensions.Configuration;
using Endatix.Infrastructure;
using Endatix.Infrastructure.Auth;
using Microsoft.Extensions.Logging;
using Endatix.Core.Abstractions;
using Endatix.Core.Features.Email;
using Endatix.Core;
using Microsoft.AspNetCore.Identity;
using Endatix.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions to install Endatix.Infrastructure services
/// For all options check <see cref="ConfigurationOptions"/>
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Uses the ILoggerFactory to create new logger instance and derive it from teh ServiceCollection's ServiceProvider
    /// </summary>
    /// <param name="services">IServiceCollection services</param>
    /// <param name="loggerName">Name of the logger</param>
    /// <returns>Logger instance</returns>
    public static ILogger CreateLogger(this IServiceCollection services, string loggerName)
    {
        Guard.Against.NullOrEmpty(services);
        Guard.Against.NullOrEmpty(loggerName);

        var serviceProvider = services.BuildServiceProvider();

        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(loggerName);
        return logger;
    }

    /// <summary>
    /// Registers services part of the Endatix.Infrastructure project and configures the services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">The action used to configure the options</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddEndatixInfrastructure(this IServiceCollection services, Action<ConfigurationOptions> configuration)
    {
        var setupConfiguration = new ConfigurationOptions();
        configuration.Invoke(setupConfiguration);

        return services.AddEndatixInfrastructure(setupConfiguration);
    }

    /// <summary>
    /// Add specific Email sender implementation, which will also register configuration for the AppSettings and configure the DI container
    /// </summary>
    /// <typeparam name="TEmailSender">Must implement <c>IEmailSender</c> & <c>IHasInstallLogic</c></typeparam>
    /// <typeparam name="TSettings">The POCO class that will be used for storing the confiuration</typeparam>
    /// <param name="services"></param>
    /// <returns>Services ready to follow the AddServices pattern</returns>
    public static IServiceCollection AddEmailSender<TEmailSender, TSettings>(this IServiceCollection services)
           where TEmailSender : class, IEmailSender, IHasConfigSection<TSettings>
           where TSettings : class
    {
        // Configure settings
        services.AddOptions<TSettings>()
                .BindConfiguration($"Email:{typeof(TSettings).Name}")
                .ValidateDataAnnotations();

        // Register the email sender and invoke the initialization delegate
        if (typeof(IPluginInitializer).IsAssignableFrom(typeof(TEmailSender)))
        {
            var initializer = typeof(TEmailSender).GetProperty(nameof(IPluginInitializer.InitializationDelegate))?.GetValue(null) as Action<IServiceCollection>;

            initializer?.Invoke(services);
        }

        services.AddScoped<IEmailSender, TEmailSender>();

        return services;
    }

    private static IServiceCollection AddEndatixInfrastructure(this IServiceCollection services, ConfigurationOptions configuration)
    {
        var logger = services.CreateLogger("EndatixInfrastructure");
        if (configuration.Security != null)
        {
            logger.LogInformation("{Component} infrastructure configuration | {Status}", "Security Config", "Started");
            var securityConfig = configuration.Security.SecurityConfiguration;
            if (configuration.Security.EnableApiAuthentication)
            {
                services.Configure<SecuritySettings>(securityConfig);

                var signingKey = securityConfig.GetRequiredSection(nameof(SecuritySettings.JwtSigningKey)).Value;

                Guard.Against.NullOrEmpty(signingKey, "signingKey", $"Cannot initialize application without a signingKey. Please check configuration for {nameof(SecuritySettings.JwtSigningKey)}");

                // services.AddIdentityCore<AppUser>()
                //     .AddEntityFrameworkStores<AppIdentityDbContext>()
                //     .AddApiEndpoints();

                // services.AddAuthorization();
                // services.AddAuthentication()
                //     .AddCookie(IdentityConstants.ApplicationScheme)
                //     .AddBearerToken(IdentityConstants.BearerScheme);

                // services.AddAuthenticationJwtBearer(s => s.SigningKey = signingKey);
                services.AddScoped<ITokenService, JwtTokenService>();
                logger.LogInformation("     >> Registering core authentication services");
            }

            if (configuration.Security.EnableDevUsersFromConfig)
            {
                services.AddScoped<IAuthService, ConfigBasedAuthService>();
                logger.LogInformation("     >> Registering {Interface} using the {ClassName} class", typeof(IAuthService).Name, typeof(ConfigBasedAuthService).Name);
            }

            logger.LogInformation("{Component} infrastructure configuration | {Status}", "Security Config", "Finished");
        }

        return services;
    }
}
