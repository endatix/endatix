using System;
using System.ComponentModel;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Logging;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Services;
using Endatix.Framework.Hosting;
using Endatix.Infrastructure;
using Endatix.Infrastructure.Auth;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Email;
using Endatix.Infrastructure.Setup;
using FastEndpoints.Security;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Endatix;

public static class EndatixHostBuilderExtensions
{
    public static IEndatixApp UseSerilogLogging(this IEndatixApp endatixApp)
    {
        endatixApp.WebHostBuilder.Host.UseSerilog((context, loggerConfig) =>
                loggerConfig.ReadFrom.Configuration(context.Configuration));

        endatixApp.LogBuilderInformation("Serilog logging configured");

        return endatixApp;
    }

    public static IEndatixApp UseDomainServices(this IEndatixApp endatixApp)
    {
        endatixApp.Services.AddScoped<IFormService, FormService>();

        return endatixApp;
    }


    public static IEndatixApp UseInfrastructure(this IEndatixApp endatixApp, Action<ConfigurationOptions> configuration)
    {
        var setupSettings = new ConfigurationOptions();
        configuration.Invoke(setupSettings);

        var services = endatixApp.Services;

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddEmailSender<SendGridEmailSender, SendGridSettings>();

        if (setupSettings.Security != null)
        {
            endatixApp.LogBuilderInformation("{Component} infrastructure configuration | {Status}", "Security Config", "Started");
            var securityConfig = setupSettings.Security.SecurityConfiguration;
            if (setupSettings.Security.EnableApiAuthentication)
            {
                services.Configure<SecuritySettings>(securityConfig);

                var signingKey = securityConfig.GetRequiredSection(nameof(SecuritySettings.JwtSigningKey)).Value;

                Guard.Against.NullOrEmpty(signingKey, "signingKey", $"Cannot initialize application without a signingKey. Please check configuration for {nameof(SecuritySettings.JwtSigningKey)}");

                services.AddAuthorization();
                services.AddAuthenticationJwtBearer(s => s.SigningKey = signingKey);
                services.AddScoped<ITokenService, JwtTokenService>();
                endatixApp.LogBuilderInformation("     >> Registering core authentication services");
            }

            if (setupSettings.Security.EnableDevUsersFromConfig)
            {
                services.AddScoped<IAuthService, ConfigBasedAuthService>();
                endatixApp.LogBuilderInformation("     >> Registering {Interface} using the {ClassName} class", typeof(IAuthService).Name, typeof(ConfigBasedAuthService).Name);
            }

            endatixApp.LogBuilderInformation("{Component} infrastructure configuration | {Status}", "Security Config", "Finished");
        }

        return endatixApp;
    }

    public static IEndatixApp UseApplicationMessaging(this IEndatixApp endatixApp, Action<MediatRConfigOptions>? options = null)
    {
        endatixApp.LogBuilderInformation("{Component} infrastructure configuration | {Status}", "MediatR", "Started");

        var services = endatixApp.Services;
        var meditROptions = new MediatRConfigOptions();
        options?.Invoke(meditROptions);

        var mediatRAssemblies = new[]
        {
            Endatix.Core.AssemblyReference.Assembly
        };

        if (meditROptions.AdditionalAssemblies.Length != 0)
        {
            mediatRAssemblies = [.. mediatRAssemblies, .. meditROptions.AdditionalAssemblies];
        }

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(mediatRAssemblies!));
        services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();

        if (meditROptions.IncludeLoggingPipeline)
        {
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingPipelineBehavior<,>));
            endatixApp.LogBuilderInformation("     >> Registering logging pipeline using the {ClassName} class", typeof(LoggingPipelineBehavior<,>).Name);
        }

        endatixApp.LogBuilderInformation("{Component} infrastructure configuration | {Status}", "MediatR", "Finished");

        return endatixApp;
    }
}
