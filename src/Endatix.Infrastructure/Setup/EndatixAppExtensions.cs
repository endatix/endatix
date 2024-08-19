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
using Serilog;

namespace Endatix.Setup;

/// <summary>
/// Provides extension methods for configuring various services and infrastructure components in the Endatix application.
/// </summary>
public static class EndatixAppExtensions
{
    /// <summary>
    /// Adds Serilog logging to the specified <see cref="IEndatixApp"/> instance.
    /// </summary>
    /// <param name="endatixApp">The <see cref="IEndatixApp"/> instance to configure.</param>
    /// <returns>The configured <see cref="IEndatixApp"/> instance.</returns>
    public static IEndatixApp AddSerilogLogging(this IEndatixApp endatixApp)
    {
        endatixApp.WebHostBuilder.Host.UseSerilog((context, loggerConfig) =>
                loggerConfig.ReadFrom.Configuration(context.Configuration));

        endatixApp.LogSetupInformation("Serilog logging configured");

        return endatixApp;
    }

    public static IEndatixApp AddDomainServices(this IEndatixApp endatixApp)
    {
        endatixApp.Services.AddScoped<IFormService, FormService>();

        return endatixApp;
    }

    /// <summary>
    /// Adds domain services to the specified <see cref="IEndatixApp"/> instance.
    /// </summary>
    /// <param name="endatixApp">The <see cref="IEndatixApp"/> instance to configure.</param>
    /// <param name="configuration">The configured <see cref="IEndatixApp"/> instance.</param>
    /// <returns></returns>
    public static IEndatixApp AddInfrastructure(this IEndatixApp endatixApp, Action<ConfigurationOptions> configuration)
    {
        var setupSettings = new ConfigurationOptions();
        configuration.Invoke(setupSettings);

        var services = endatixApp.Services;

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddEmailSender<SendGridEmailSender, SendGridSettings>();

        if (setupSettings.Security != null)
        {
            endatixApp.LogSetupInformation("{Component} infrastructure configuration | {Status}", "Security Config", "Started");
            var securityConfig = setupSettings.Security.SecurityConfiguration;
            if (setupSettings.Security.EnableApiAuthentication)
            {
                services.Configure<SecuritySettings>(securityConfig);

                var signingKey = securityConfig.GetRequiredSection(nameof(SecuritySettings.JwtSigningKey)).Value;

                Guard.Against.NullOrEmpty(signingKey, "signingKey", $"Cannot initialize application without a signingKey. Please check configuration for {nameof(SecuritySettings.JwtSigningKey)}");

                services.AddAuthorization();
                services.AddAuthenticationJwtBearer(s => s.SigningKey = signingKey);
                services.AddScoped<ITokenService, JwtTokenService>();
                endatixApp.LogSetupInformation("     >> Registering core authentication services");
            }

            if (setupSettings.Security.EnableDevUsersFromConfig)
            {
                services.AddScoped<IAuthService, ConfigBasedAuthService>();
                endatixApp.LogSetupInformation("     >> Registering {Interface} using the {ClassName} class", typeof(IAuthService).Name, typeof(ConfigBasedAuthService).Name);
            }

            endatixApp.LogSetupInformation("{Component} infrastructure configuration | {Status}", "Security Config", "Finished");
        }

        return endatixApp;
    }

    /// <summary>
    /// Adds infrastructure services to the specified <see cref="IEndatixApp"/> instance, including security and email services, based of specified configuration options.
    /// </summary>
    /// <param name="endatixApp">The <see cref="IEndatixApp"/> instance to configure.</param>
    /// <param name="options">A delegate to configure the infrastructure options.</param>
    /// <returns>The configured <see cref="IEndatixApp"/> instance.</returns>
    public static IEndatixApp AddApplicationMessaging(this IEndatixApp endatixApp, Action<MediatRConfigOptions>? options = null)
    {
        endatixApp.LogSetupInformation("{Component} infrastructure configuration | {Status}", "MediatR", "Started");

        var meditROptions = new MediatRConfigOptions();
        options?.Invoke(meditROptions);

        var services = endatixApp.Services;
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
            endatixApp.LogSetupInformation("     >> Registering logging pipeline using the {ClassName} class", typeof(LoggingPipelineBehavior<,>).Name);
        }

        endatixApp.LogSetupInformation("{Component} infrastructure configuration | {Status}", "MediatR", "Finished");

        return endatixApp;
    }
}
